using System.Net.Http;
using System.Threading.Tasks;
using System.Management; // Requires adding <PackageReference Include="System.Management" Version="8.0.0" /> to the .csproj
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using it_beacon_systray.Models; // For the API models
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

namespace it_beacon_systray.Helpers
{
    /// <summary>
    /// Provides static methods for fetching system information and calling external APIs.
    /// </summary>
    public static class SystemInfoHelper
    {
        // Use a single static HttpClient instance
        private static readonly HttpClient Client = new HttpClient();

        /// <summary>
        /// Gets the machine's Service Tag (Serial Number) from WMI.
        /// </summary>
        public static string GetMachineServiceTag()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                using (var collection = searcher.Get())
                {
                    return collection.OfType<ManagementObject>().Select(mo => mo["SerialNumber"]?.ToString() ?? "").FirstOrDefault() ?? "";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemInfoHelper.GetMachineServiceTag] WMI Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Fetches asset location from the Snipe-IT API.
        /// </summary>
        public static async Task<SnipeItLocation?> FetchSnipeItLocationAsync(string serviceTag, string apiUrl, string apiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/{serviceTag}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("Accept", "application/json");

            var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var snipeItData = JsonSerializer.Deserialize<SnipeItAssetResponse>(jsonResponse);

            return snipeItData?.rows?.FirstOrDefault()?.location;
        }

        /// <summary>
        /// Fetches the Risk Score for a given hostname from the API.
        /// </summary>
        /// <returns>Risk score, or null if not found.</returns>
        public static async Task<int?> FetchRiskScoreAsync(string hostname, string apiUrl)
        {
            var jsonResponse = await Client.GetStringAsync(apiUrl);
            var responseData = JsonSerializer.Deserialize<SyncedAssetsResponse>(jsonResponse);

            var matchingAsset = responseData?.assets?.FirstOrDefault(a => a.Hostname.Equals(hostname, System.StringComparison.OrdinalIgnoreCase));

            if (matchingAsset != null)
            {
                return matchingAsset.RiskScore;
            }

            return null; // Not found
        }


        /// <summary>
        /// Gathers network info (local IP, public IP, connection type) and formats it.
        /// </summary>
        public static async Task<NetworkInfo> GetNetworkInfoAsync()
        {
            string connectionType = "Unknown";
            string publicIpString = "N/A";
            IPAddress? localIp = null;
            var tooltipBuilder = new StringBuilder();

            try
            {
                // 1. Find the primary active network interface
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                           ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                           ni.GetIPProperties().GatewayAddresses.Any());

                if (activeInterface != null)
                {
                    var ipProps = activeInterface.GetIPProperties();
                    localIp = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

                    // 2. Get connection type
                    connectionType = activeInterface.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => "Ethernet",
                        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                        _ => activeInterface.NetworkInterfaceType.ToString(),
                    };
                }

                // 3. Get the public IP
                publicIpString = await Client.GetStringAsync("https://api.ipify.org");
                publicIpString = publicIpString.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemInfoHelper.GetNetworkInfoAsync] Error: {ex.Message}");
            }

            // 4. Format the output
            var result = new NetworkInfo();
            if (localIp != null && IsPrivateIpAddress(localIp))
            {
                // On a private/corporate network
                result.DisplayIp = localIp.ToString();
                tooltipBuilder.AppendLine($"Public IP: {publicIpString}");
            }
            else
            {
                // On a public network (or no private IP found)
                result.DisplayIp = publicIpString;
            }

            tooltipBuilder.Append($"Connection: {connectionType}");
            result.Tooltip = tooltipBuilder.ToString();

            return result;
        }

        /// <summary>
        /// Checks if an IP address is within the private (RFC 1918) address ranges.
        /// </summary>
        private static bool IsPrivateIpAddress(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();
            switch (bytes[0])
            {
                case 10: // 10.0.0.0/8
                    return true;
                case 172: // 172.16.0.0/12
                    return bytes[1] >= 16 && bytes[1] < 32;
                case 192: // 192.168.0.0/16
                    return bytes[1] == 168;
                default:
                    return false;
            }
        }
    }
}

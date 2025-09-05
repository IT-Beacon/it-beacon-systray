using Hardcodet.Wpf.TaskbarNotification;
using it_beacon_common.Helpers; // Import the ThemeHelper
using it_beacon_systray.Views;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static it_beacon_systray.Views.PopupWindow;

namespace it_beacon_systray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // --- NESTED HELPER CLASSES ---
        // These helpers are now correctly placed inside the App class.

        public static class ApiHelper
        {
            public static readonly HttpClient Client = new HttpClient();
        }

        public class Asset
        {
            [JsonPropertyName("Hostname")]
            public string Hostname { get; set; } = string.Empty;

            [JsonPropertyName("RiskScore")]
            public string RiskScore { get; set; } = string.Empty;
        }

        // --- END OF NESTED CLASSES ---

        private TaskbarIcon? _notifyIcon;
        private PopupWindow? _popupWindow;
        private DispatcherTimer? _riskScoreTimer;

        // Public property to store the timestamp of the last update.
        public DateTime? LastRiskScoreUpdate { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- FIX FOR DARK MODE ---
            // 1. Apply the current Windows theme (light or dark) when the app starts.
            ThemeHelper.ApplyTheme();

            // 2. Start listening for any future theme changes made by the user.
            ThemeHelper.RegisterThemeChangeListener();
            // --- END OF FIX ---

            // Initialize the popup window and tray icon
            _popupWindow = new PopupWindow();
            InitializeTrayIcon();

            // Fetch initial data when the app starts
            await FetchAndSetRiskScoreAsync();

            // Set up a timer to periodically refresh the risk score every 30 minutes
            _riskScoreTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _riskScoreTimer.Tick += async (s, args) => await FetchAndSetRiskScoreAsync();
            _riskScoreTimer.Start();
        }

        private void InitializeTrayIcon()
        {
            var trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            _notifyIcon = trayIcon;

            // Set a default icon to start
            UpdateTrayIcon(0); // Default to green

            // Wire up events
            _notifyIcon.TrayLeftMouseUp += (s, ev) => _popupWindow?.ToggleVisibility();
            _notifyIcon.TrayRightMouseUp += (s, ev) => _popupWindow?.ToggleVisibility();
        }

        /// <summary>
        /// Intelligently determines the IP to display and builds a simplified tooltip.
        /// </summary>
        public async Task FetchAndSetIpAddressAsync()
        {
            if (_popupWindow == null) return;

            try
            {
                // --- Gather network information ---
                string connectionType = "Unknown";

                // 1. Find the primary active network interface
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                           ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                           ni.GetIPProperties().GatewayAddresses.Any());

                IPAddress? localIp = null;

                if (activeInterface != null)
                {
                    var ipProps = activeInterface.GetIPProperties();
                    localIp = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

                    // 2. Get connection type from the active interface
                    connectionType = activeInterface.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => "Ethernet",
                        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                        _ => activeInterface.NetworkInterfaceType.ToString(),
                    };
                }


                // 3. Get the public IP from an external service
                var publicIpString = await ApiHelper.Client.GetStringAsync("https://api.ipify.org");


                // --- Now, update the UI based on the gathered information ---

                var tooltipBuilder = new StringBuilder();

                if (localIp != null && IsPrivateIpAddress(localIp))
                {
                    // --- ON A CORPORATE/PRIVATE NETWORK ---
                    _popupWindow.NetworkValue.Text = localIp.ToString();

                    tooltipBuilder.AppendLine($"Public IP: {publicIpString.Trim()}");
                    tooltipBuilder.Append($"Connection: {connectionType}");
                }
                else
                {
                    // --- ON A PUBLIC NETWORK ---
                    _popupWindow.NetworkValue.Text = publicIpString.Trim();
                    tooltipBuilder.Append($"Connection: {connectionType}");
                }

                _popupWindow.NetworkBorder.ToolTip = tooltipBuilder.ToString();
            }
            catch (Exception)
            {
                _popupWindow.NetworkValue.Text = "Error";
                _popupWindow.NetworkBorder.ToolTip = "Could not determine connection";
            }
        }

        /// <summary>
        /// Checks if an IP address is within the private (RFC 1918) address ranges.
        /// </summary>
        private bool IsPrivateIpAddress(IPAddress ipAddress)
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

        /// <summary>
        /// Fetches the risk score from the web service.
        /// This is the central method for all risk score updates.
        /// </summary>
        public async Task FetchAndSetRiskScoreAsync()
        {
            if (_popupWindow == null) return;

            var currentHostname = _popupWindow.HostnameValue.Text;
            _popupWindow.RiskScoreValue.Text = "Refreshing...";

            try
            {
                var jsonResponse = await ApiHelper.Client.GetStringAsync("https://public.cvad.unt.edu/rapid7/cvad-tagged-assets.json");
                var assets = JsonSerializer.Deserialize<Asset[]>(jsonResponse);
                var matchingAsset = assets?.FirstOrDefault(a => a.Hostname.Equals(currentHostname, StringComparison.OrdinalIgnoreCase));

                if (matchingAsset != null && int.TryParse(matchingAsset.RiskScore, out int score))
                {
                    _popupWindow.RiskScoreValue.Text = score.ToString("N0");
                    UpdateTrayIcon(score);
                    // Set the timestamp on successful update.
                    LastRiskScoreUpdate = DateTime.Now;
                }
                else
                {
                    _popupWindow.RiskScoreValue.Text = "Not Found";
                    UpdateTrayIcon(-1);
                    LastRiskScoreUpdate = DateTime.Now; // Also update timestamp on a "not found" result
                }
            }
            catch (Exception)
            {
                _popupWindow.RiskScoreValue.Text = "Error";
                UpdateTrayIcon(-1);
                LastRiskScoreUpdate = DateTime.Now; // Also update timestamp on error
            }
        }

        /// <summary>
        /// Updates the system tray icon based on the provided risk score.
        /// </summary>
        /// <param name="riskScore">The current risk score.</param>
        public void UpdateTrayIcon(int riskScore)
        {
            if (_notifyIcon == null) return;

            string iconName;
            if (riskScore < 0) // Error or Not Found
            {
                iconName = "beacon-blue.ico";
            }
            else if (riskScore <= 1000)
            {
                iconName = "beacon-green.ico";
            }
            else if (riskScore <= 5000)
            {
                iconName = "beacon-yellow.ico";
            }
            else
            {
                iconName = "beacon-red.ico";
            }

            var iconUri = new Uri($"pack://application:,,,/it-beacon-common;component/Assets/Icons/{iconName}");
            _notifyIcon.IconSource = new BitmapImage(iconUri);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _riskScoreTimer?.Stop();
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

        private void TogglePopup()
        {
            if (_popupWindow == null) return;

            if (_popupWindow.IsVisible)
            {
                _popupWindow.Hide();
            }
            else
            {
                _popupWindow.PositionNearTray();
                _popupWindow.Show();
                _popupWindow.Activate();
            }
        }

        private void OpenNotepad()
        {
            Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        }

    }
}
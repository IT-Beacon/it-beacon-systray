using Hardcodet.Wpf.TaskbarNotification;
using it_beacon_common.Helpers;
using it_beacon_systray.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace it_beacon_systray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // A unique name for the Mutex to ensure only one instance of the app runs per user.
        private const string AppMutexName = "IT-BEACON-SYSTRAY-INSTANCE";
        private static Mutex? _mutex;

        // --- IMPORTANT: PASTE YOUR SNIPE-IT API KEY HERE ---
        private readonly string _snipeItApiKey = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJhdWQiOiIxIiwianRpIjoiZmNkMTk1OGEwZDc1MTNjNTEzZWQ0NThlZTk5OWE5NmVhYjgwM2U4MTEzNGY4MWE3ZmM1ZGI1MjhiODMzNDE2YWQ4YzA5ODVkYTgyNGM0OTQiLCJpYXQiOjE3NTczNjQzMDAuNzU4NzU1LCJuYmYiOjE3NTczNjQzMDAuNzU4NzU4LCJleHAiOjIzODg1MTYzMDAuNzQwNTQyLCJzdWIiOiIxIiwic2NvcGVzIjpbXX0.MYtoUjhKXH4k73NqHmtbs657nt06B6bweIceKzQCWDzcWDDsbnkACwDSzYPpw6HLuyTQPjv8bsNS82nlO4GsIPt2mqJZR4MLWv8bwEMFzxuyAqJg5uwZFOPVZO0wEFjidI5Gg_n8ke4V8EztqTEh8wQbM8d2qvqjiBtF9auItfYNjWLthWXYLdTXsfeH6bCvQ3Oh5NsPdrNlkGq7iN2DF1kKWGrVVCKq1hfYEv0fTqybRrPAVpzIhkI0fVAKkAlVpR2_7BWr6kLDzuhi3iztSyIaEthFpgITSpqMFz3NYaGuVloQvl-D5I4a8sD70PPmj7R0RjjV74FLHDmk7O2AEY-ze4AtMxEes3Wh9DFuf6Jzsb4jNIYjw7CvPBLBy34ClhD9XKXL4xEbetlMq9znQOfzuj6i70Gbp0KAm7BTWkfaLurNPDjWbZIejH1-trLhKwih1VxlVq7kl52M-mH0HqI-oW22GNxQUyvPYwPKoO3sl0B71W4ho5illHQFDtGlbxfGJ11RG6SrMrLwVLih_wmjWIPSBdok4aknA4Tu9nI2Ux3qJbKTcoArgRZVcg7Mof0gqdxteM7tv5jDu_XhAmHn3Oq1RmTb848w4iqlXeT1sZ4dUXUYL-vaaalMMEhp4yY0tUaqqfKlXIFeLD1TQoimgoLUTBzq3eM9pRLKQbM";
        // ---

        // --- NESTED HELPER CLASSES ---
        public class SnipeItAssetResponse { public List<SnipeItAsset>? rows { get; set; } }
        public class SnipeItAsset { public string? name { get; set; } public SnipeItLocation? location { get; set; } }
        public class SnipeItLocation { public string? name { get; set; } }


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

        // These are now class-level fields, making them accessible throughout the App class.
        private TaskbarIcon? _notifyIcon;
        private PopupWindow? _popupWindow;
        private DispatcherTimer? _riskScoreTimer;

        // Public property to store the timestamp of the last update.
        public DateTime? LastRiskScoreUpdate { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // --- NEW: SINGLE INSTANCE CHECK USING MUTEX ---
            _mutex = new Mutex(true, AppMutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running. Shut down this new instance.
                Debug.WriteLine("[App.OnStartup] Another instance is already running. Shutting down.");
                Application.Current.Shutdown();
                return;
            }
            // --- END OF SINGLE INSTANCE CHECK ---

            // These handlers will catch any unhandled exceptions and restart the application.
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            // --- END OF CRASH DETECTION ---

            // --- NEW STARTUP LOGIC ---
            using (var identity = WindowsIdentity.GetCurrent())
            {
                if (identity.IsSystem)
                {
                    // If running as SYSTEM, launch into user session and exit this instance.
                    Debug.WriteLine("[App.OnStartup] Running as SYSTEM. Attempting to relaunch in user session.");
                    ProcessLauncher.StartProcessInUserSession();
                    Application.Current.Shutdown();
                    return; // Stop execution of the SYSTEM process
                }
            }
            // --- END OF NEW LOGIC ---

            // If not running as SYSTEM, proceed with normal application startup.
            Debug.WriteLine("[App.OnStartup] Running as standard user. Initializing application.");
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
            await FetchSnipeItDataAsync(); 

            // Set up a timer to periodically refresh the risk score every 30 minutes
            _riskScoreTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _riskScoreTimer.Tick += async (s, args) => await FetchAndSetRiskScoreAsync();
            _riskScoreTimer.Start();
        }

        /// <summary>
        /// Handles exceptions that occur on the main UI thread.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
            e.Handled = true; // Prevents the default Windows crash dialog
        }

        /// <summary>
        /// Handles exceptions that occur on any thread (background tasks, etc.).
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleUnhandledException(ex);
            }
        }

        /// <summary>
        /// Central logic for logging the crash and restarting the application.
        /// </summary>
        private void HandleUnhandledException(Exception ex)
        {
            // Log the exception details for debugging purposes
            Debug.WriteLine($"[FATAL CRASH] An unhandled exception occurred: {ex.Message}\n{ex.StackTrace}");

            // Attempt to restart the application
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
            }
            catch (Exception restartEx)
            {
                Debug.WriteLine($"[FATAL CRASH] Failed to restart the application: {restartEx.Message}");
            }

            // Shut down the crashed instance
            Application.Current.Shutdown();
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
        /// Fetches asset location from the Snipe-IT API using the machine's Service Tag.
        /// </summary>
        public async Task FetchSnipeItDataAsync()
        {
            if (_popupWindow == null) return;
            _popupWindow.LocationValue.Text = "Fetching...";

            try
            {
                // Get the Service Tag (Serial Number) from WMI.
                string serviceTag = string.Empty;
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                using (var collection = searcher.Get())
                {
                    serviceTag = collection.OfType<ManagementObject>().Select(mo => mo["SerialNumber"]?.ToString() ?? "").FirstOrDefault() ?? "";
                }

                if (string.IsNullOrWhiteSpace(serviceTag))
                {
                    _popupWindow.LocationValue.Text = "No S/N";
                    return;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, $"https://inventory.cvad.unt.edu/api/v1/hardware/byserial/{serviceTag}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _snipeItApiKey);
                request.Headers.Add("Accept", "application/json");

                var response = await ApiHelper.Client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var snipeItData = JsonSerializer.Deserialize<SnipeItAssetResponse>(jsonResponse);

                var asset = snipeItData?.rows?.FirstOrDefault();

                // Update Location UI
                _popupWindow.LocationValue.Text = asset?.location?.name ?? "Not Found";
            }
            catch (Exception)
            {
                _popupWindow.LocationValue.Text = "Error";
            }
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

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop the timer to prevent it from ticking after shutdown.
            _riskScoreTimer?.Stop();

            // Dispose of the tray icon to remove it from the system tray.
            _notifyIcon?.Dispose();

            // Release and dispose of the Mutex to allow a new instance to start.
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            base.OnExit(e);
        }


    }
}
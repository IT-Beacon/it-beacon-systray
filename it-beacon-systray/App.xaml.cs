using Hardcodet.Wpf.TaskbarNotification;
using it_beacon_common.Config;
using it_beacon_common.Helpers;
// --- NEW: Using statements for new helper and model files ---
using it_beacon_systray.Helpers;
using it_beacon_systray.Models;
using it_beacon_systray.Views;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
// --- ADD THIS USING ALIAS TO FIX AMBIGUITY ---
using ThemeHelper = it_beacon_common.Helpers.ThemeHelper;

namespace it_beacon_systray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Fields

        // A unique name for the Mutex to ensure only one instance of the app runs per user.
        private const string AppMutexName = "IT-BEACON-SYSTRAY-INSTANCE";
        private static Mutex? _mutex;

        // These are class-level fields, making them accessible throughout the App class.
        private TaskbarIcon? _notifyIcon;
        private PopupWindow? _popupWindow;
        private DispatcherTimer? _refreshTimer; // Renamed from _riskScoreTimer for clarity

        // Public property to store the timestamp of the last update.
        public DateTime? LastRiskScoreUpdate { get; private set; }

        // --- FIELDS FOR RESTART REMINDER ---
        private ReminderOverlayWindow? _reminderWindow;
        private int _restartDeferenceCount = 0; // Tracks "Restart Later" clicks
        private DateTime? _restartCooldownUntil = null; // Timestamp for the cooldown

        #endregion

        #region Application Lifecycle & Startup

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Load configuration first
            ConfigManager.LoadConfig();

            // 2. Ensure only one instance is running
            if (!IsFirstInstance())
            {
                Debug.WriteLine("[App.OnStartup] Another instance is already running. Shutting down.");
                Application.Current.Shutdown();
                return;
            }

            // 3. Set up global crash handlers
            RegisterCrashHandlers();

            // 4. Check if running as SYSTEM and relaunch if necessary
            if (IsRunningAsSystem())
            {
                Debug.WriteLine("[App.OnStartup] Running as SYSTEM. Attempting to relaunch in user session.");
                ProcessLauncher.StartProcessInUserSession();
                Application.Current.Shutdown();
                return; // Stop execution of the SYSTEM process
            }

            // 5. If all checks pass, proceed with normal application startup.
            Debug.WriteLine("[App.OnStartup] Running as standard user. Initializing application.");
            base.OnStartup(e);
            await InitializeMainApplication();
        }

        /// <summary>
        /// Contains the main initialization logic for the application
        /// after all pre-checks (mutex, SYSTEM user) have passed.
        /// </summary>
        private async Task InitializeMainApplication()
        {
            // 1. Apply the current Windows theme (light or dark) when the app starts.
            ThemeHelper.ApplyTheme();

            // 2. Start listening for any future theme changes made by the user.
            ThemeHelper.RegisterThemeChangeListener();

            // 3. Initialize the popup window and tray icon
            _popupWindow = new PopupWindow();
            InitializeTrayIcon();

            // 4. Fetch initial data when the app starts
            await FetchAndSetRiskScoreAsync();
            await FetchSnipeItDataAsync();
            await FetchAndSetIpAddressAsync();

            // 5. Set up the main refresh timer
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();

            // 6. Perform an initial uptime check on startup
            // This catches cases where the app is launched after the trigger time has already passed.
            CheckUptimeTrigger(null, EventArgs.Empty);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop the timer to prevent it from ticking after shutdown.
            _refreshTimer?.Stop();

            // Dispose of the tray icon to remove it from the system tray.
            _notifyIcon?.Dispose();

            // Release and dispose of the Mutex to allow a new instance to start.
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            base.OnExit(e);
        }

        #endregion

        #region Singleton & Process Logic

        /// <summary>
        /// Checks if this is the first instance of the application.
        /// </summary>
        private bool IsFirstInstance()
        {
            _mutex = new Mutex(true, AppMutexName, out bool createdNew);
            return createdNew;
        }

        /// <summary>
        /// Checks if the application is currently running as the SYSTEM user.
        /// </summary>
        private bool IsRunningAsSystem()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return identity.IsSystem;
            }
        }

        #endregion

        #region Timer Management

        /// <summary>
        /// --- NEW: Single handler for the refresh timer ---
        /// Called periodically to refresh risk score and check uptime.
        /// Interval is dynamic: 30 mins normally, or CooldownTime if on cooldown.
        /// </summary>
        private async void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            await FetchAndSetRiskScoreAsync();
            CheckUptimeTrigger(sender, e);

            // --- NEW: Reset timer to default interval if it was on a cooldown ---
            if (_refreshTimer != null && _refreshTimer.Interval != TimeSpan.FromMinutes(30))
            {
                _refreshTimer.Stop();
                _refreshTimer.Interval = TimeSpan.FromMinutes(30);
                _refreshTimer.Start();
                Debug.WriteLine("[App.OnRefreshTimerTick] Reset refresh timer interval to 30 minutes.");
            }
        }

        #endregion

        #region Exception Handling

        /// <summary>
        /// Registers global exception handlers to catch unhandled exceptions.
        /// </summary>
        private void RegisterCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
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
        #endregion

        #region Tray Icon

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

        /// <summary>
        /// Handles the "Settings" menu item click.
        /// </summary>
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = _popupWindow };
            settingsWindow.ShowDialog();
        }

        /// <summary>
        /// Handles the "Test Reminder" menu item click.
        /// </summary>
        private void TestReminderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowReminderOverlay();
        }

        /// <summary>
        /// Fired when the tray icon's context menu is about to open.
        /// Used to dynamically show/hide menu items based on settings.
        /// </summary>
        private void ContextMenu_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // This assumes "TestReminderMenuItem" is the x:Name in your TrayIcon.xaml
                if (FindResource("TestReminderMenuItem") is MenuItem testMenuItem)
                {
                    // Read the setting from ConfigManager
                    bool isReminderEnabled = ConfigManager.GetBool("/Settings/ReminderOverlay/Enabled", true);

                    // Set the visibility of the "Test Reminder" menu item
                    testMenuItem.Visibility = isReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenu_Loaded] Error updating menu item visibility: {ex.Message}");
            }
        }

        #endregion

        #region Reminder Overlay Logic
        /// <summary>
        /// Checks system uptime and shows the restart reminder if uptime is >= trigger time
        /// and the app is not on cooldown.
        /// </summary>
        private void CheckUptimeTrigger(object? sender, EventArgs e)
        {
            // 1. Check if feature is enabled
            if (ConfigManager.GetBool("/Settings/ReminderOverlay/Enabled") == false)
            {
                return;
            }

            // 2. Don't check if window is already open
            if (_reminderWindow != null && _reminderWindow.IsVisible)
            {
                return;
            }

            // 3. Check for cooldown
            if (DateTime.Now < _restartCooldownUntil)
            {
                Debug.WriteLine($"[App.CheckUptimeTrigger] On cooldown. Skipping check until {_restartCooldownUntil}.");
                return; // We are on cooldown, do nothing
            }

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            // 4. Get trigger minutes from config
            int triggerMinutes = ConfigManager.GetInt("/Settings/ReminderOverlay/TriggerTime", 10080); // Default: 7 days

            // 5. Check if we need to show the reminder
            if (uptime.TotalMinutes >= triggerMinutes)
            {
                Debug.WriteLine($"[App.CheckUptimeTrigger] Uptime >= {triggerMinutes} minutes. Showing reminder.");
                ShowReminderOverlay(uptime); // Show the overlay
            }
            // 6. Check if we need to reset the counter (machine was restarted)
            else if (uptime.TotalDays < 1) // Using 1 day as a safe "restarted" threshold
            {
                if (_restartDeferenceCount > 0 || _restartCooldownUntil.HasValue)
                {
                    Debug.WriteLine("[App.CheckUptimeTrigger] Uptime is low, resetting deference count and cooldown.");
                    _restartDeferenceCount = 0;
                    _restartCooldownUntil = null;
                }
            }
        }

        /// <summary>
        /// Displays the Restart Reminder Overlay window.
        /// Can be called manually for testing.
        /// </summary>
        public void ShowReminderOverlay(TimeSpan? uptime = null)
        {
            // Ensure this always runs on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowReminderOverlay(uptime));
                return;
            }

            // If window is already open, just activate it
            if (_reminderWindow != null && _reminderWindow.IsVisible)
            {
                _reminderWindow.Activate();
                return;
            }

            // Get custom values from Config
            string message = ConfigManager.GetString("/Settings/ReminderOverlay/Message",
                "Your computer has been running for a long time without a restart. " +
                "To ensure system stability and apply updates, please restart your machine.");

            int cooldownMinutes = ConfigManager.GetInt("/Settings/ReminderOverlay/CooldownTime", 360); // Default: 6 hours

            // Pass new values to the window
            _reminderWindow = new ReminderOverlayWindow(_restartDeferenceCount, message, cooldownMinutes);

            // Null out the reference when the window is closed
            _reminderWindow.Closed += (s, e) => _reminderWindow = null;

            _reminderWindow.Show();
            _reminderWindow.Activate();
        }

        /// <summary>
        /// Registers a deference request from the overlay window.
        /// Increments the counter (normal click) or resets it to 0 (Shift-click).
        /// Sets a cooldown in all cases.
        /// </summary>
        /// <param name="isReset">True if Shift was held, resetting the counter.</param>
        public void RegisterDeference(bool isReset)
        {
            if (isReset)
            {
                // Shift-click: Reset the counter to 0
                _restartDeferenceCount = 0;
                Debug.WriteLine($"[App.RegisterDeference] Deference counter reset to 0 (Shift-click).");
            }
            else
            {
                // Normal click: Increment the counter
                _restartDeferenceCount++;
                Debug.WriteLine($"[App.RegisterDeference] Deference registered. New count: {_restartDeferenceCount}");
            }

            // Get cooldown from config
            int cooldownMinutes = ConfigManager.GetInt("/Settings/ReminderOverlay/CooldownTime", 360);
            _restartCooldownUntil = DateTime.Now.AddMinutes(cooldownMinutes);
            Debug.WriteLine($"[App.RegisterDeference] Cooldown set until: {_restartCooldownUntil}");

            // --- NEW: Dynamically change timer interval to match cooldown ---
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                // Set timer to fire just after cooldown expires (with a 5-sec buffer)
                TimeSpan newInterval = TimeSpan.FromMinutes(cooldownMinutes).Add(TimeSpan.FromSeconds(5));
                _refreshTimer.Interval = newInterval;
                _refreshTimer.Start();
                Debug.WriteLine($"[App.RegisterDeference] Refresh timer interval set to {newInterval.TotalMinutes} minutes.");
            }
        }
        #endregion

        #region Data Fetching

        /// <summary>
        /// Fetches asset location from the Snipe-IT API using the machine's Service Tag.
        /// </summary>
        public async Task FetchSnipeItDataAsync()
        {
            if (_popupWindow == null) return;

            if (ConfigManager.GetBool("/Settings/PopupWindow/ShowLocation") == false)
            {
                return; // Do not fetch if panel is disabled
            }

            _popupWindow.LocationValue.Text = "Fetching...";

            try
            {
                // 1. Get Service Tag (Serial Number) from helper
                string serviceTag = SystemInfoHelper.GetMachineServiceTag();

                if (string.IsNullOrWhiteSpace(serviceTag))
                {
                    _popupWindow.LocationValue.Text = "No S/N";
                    return;
                }

                // 2. Read config for API
                string snipeItUrl = ConfigManager.GetString("/Settings/SnipeIT/ApiUrl", "https://inventory.cvad.unt.edu/api/v1/hardware/byserial");
                string snipeItApiKey = ConfigManager.GetString("/Settings/SnipeIT/ApiKey");

                if (string.IsNullOrEmpty(snipeItApiKey))
                {
                    Debug.WriteLine("[App.FetchSnipeItDataAsync] SnipeIT API Key is missing from settings.xml");
                    _popupWindow.LocationValue.Text = "Config Error";
                    return;
                }

                // 3. Fetch data using helper
                var location = await SystemInfoHelper.FetchSnipeItLocationAsync(serviceTag, snipeItUrl, snipeItApiKey);

                // 4. Update Location UI
                _popupWindow.LocationValue.Text = location?.name ?? "Not Found";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App.FetchSnipeItDataAsync] Error: {ex.Message}");
                _popupWindow.LocationValue.Text = "Error";
            }
        }


        /// <summary>
        /// Intelligently determines the IP to display and builds a simplified tooltip.
        /// </summary>
        public async Task FetchAndSetIpAddressAsync()
        {
            if (_popupWindow == null) return;

            if (ConfigManager.GetBool("/Settings/PopupWindow/ShowNetworkInfo") == false)
            {
                return; // Do not fetch if panel is disabled
            }

            try
            {
                // 1. Fetch network info from helper
                var netInfo = await SystemInfoHelper.GetNetworkInfoAsync();

                // 2. Update the UI
                _popupWindow.NetworkValue.Text = netInfo.DisplayIp;
                _popupWindow.NetworkBorder.ToolTip = netInfo.Tooltip;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App.FetchAndSetIpAddressAsync] Error: {ex.Message}");
                _popupWindow.NetworkValue.Text = "Error";
                _popupWindow.NetworkBorder.ToolTip = "Could not determine connection";
            }
        }

        /// <summary>
        /// Fetches the risk score from the web service.
        /// This is the central method for all risk score updates.
        /// </summary>
        public async Task FetchAndSetRiskScoreAsync()
        {
            if (_popupWindow == null) return;

            if (ConfigManager.GetBool("/Settings/PopupWindow/ShowRiskScore") == false)
            {
                return; // Do not fetch if panel is disabled
            }

            var currentHostname = _popupWindow.HostnameValue.Text;
            _popupWindow.RiskScoreValue.Text = "Refreshing...";
            try
            {
                // 1. Read config for API URL
                // --- NOTE: This URL was hardcoded. You may want to move it to settings.xml ---
                string riskScoreUrl = "https://itservices.cvad.unt.edu/it-tray/synced-assets-mini.json";

                // 2. Fetch data using helper
                var score = await SystemInfoHelper.FetchRiskScoreAsync(currentHostname, riskScoreUrl);

                if (score.HasValue)
                {
                    _popupWindow.RiskScoreValue.Text = score.Value.ToString("N0");
                    UpdateTrayIcon(score.Value);
                    LastRiskScoreUpdate = DateTime.Now;
                }
                else
                {
                    _popupWindow.RiskScoreValue.Text = "Not Found";
                    UpdateTrayIcon(-1);
                    LastRiskScoreUpdate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App.FetchAndSetRiskScoreAsync] Error: {ex.Message}");
                _popupWindow.RiskScoreValue.Text = "Error";
                UpdateTrayIcon(-1);
                LastRiskScoreUpdate = DateTime.Now;
            }
        }
        #endregion
    }
}


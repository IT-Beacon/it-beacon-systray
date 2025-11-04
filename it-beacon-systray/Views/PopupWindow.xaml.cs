using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement; // Added for retrieving user display name
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using it_beacon_common.Config;
using System.Windows.Data;
using System.Windows.Media; // --- THIS IS NEEDED FOR SetResourceReference ---
using it_beacon_systray.Views;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    ///

    public partial class PopupWindow : Window
    {
        private readonly DispatcherTimer _uptimeTimer;

        private SettingsWindow? _settingsWindow;

        public PopupWindow()
        {
            InitializeComponent();
            this.Deactivated += (s, e) => this.Hide();
            this.IsVisibleChanged += PopupWindow_IsVisibleChanged;


            // Set static values on startup
            HostnameValue.Text = Environment.MachineName;
            SetUserDisplayName();

            // Set the user's display name
            try
            {
                // This gets the full name (e.g., "John Doe") instead of just the username
                UserNameValue.Text = UserPrincipal.Current.DisplayName;
            }
            catch (Exception)
            {
                // Fallback to the simpler username if the display name can't be found
                UserNameValue.Text = Environment.UserName;
            }

            // --- Apply config on init ---
            ApplyConfiguration();
            // ---

            // Initialize the timer for the live uptime counter
            _uptimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uptimeTimer.Tick += UptimeTimer_Tick;
        }

        /// <summary>
        /// Reads settings from ConfigManager and updates the UI visibility and buttons.
        /// </summary>
        private void ApplyConfiguration()
        {
            // Set panel visibility
            NetworkInfoPanel.Visibility = ConfigManager.GetBool("/Settings/PopupWindow/ShowNetworkInfo", true)
                ? Visibility.Visible : Visibility.Collapsed;

            RiskScorePanel.Visibility = ConfigManager.GetBool("/Settings/PopupWindow/ShowRiskScore", true)
                ? Visibility.Visible : Visibility.Collapsed;

            UptimePanel.Visibility = ConfigManager.GetBool("/Settings/PopupWindow/ShowUptime", true)
                ? Visibility.Visible : Visibility.Collapsed;

            LocationPanel.Visibility = ConfigManager.GetBool("/Settings/PopupWindow/ShowLocation", true)
                ? Visibility.Visible : Visibility.Collapsed;

            // Generate Quick Shortcut buttons
            GenerateQuickShortcutButtons();
        }

        /// <summary>
        /// Clears and repopulates the Quick Shortcut panel from config.
        /// </summary>
        private void GenerateQuickShortcutButtons()
        {
            QUICK_SHORTCUT_PANEL.Children.Clear();
            var shortcuts = ConfigManager.GetQuickShortcuts();

            foreach (var shortcut in shortcuts)
            {
                var button = new Button
                {
                    ToolTip = shortcut.ToolTip,
                    Tag = shortcut.Url, // Store URL in Tag for the click event
                    Style = (Style)FindResource("IconButtonStyle")
                };

                var textBlock = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    Text = shortcut.Glyph, // This is correct! It already contains "&#xE8F2;"
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.8
                };

                // --- THIS IS THE FIX (for dark mode color) ---
                // Set the resource reference directly to the theme's brush.
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "PopupForegroundBrush");
                // --- END OF FIX ---

                button.Content = textBlock;
                button.Click += QuickShortcut_Click; // Wire up the generic click handler

                QUICK_SHORTCUT_PANEL.Children.Add(button);
            }
        }

        /// <summary>
        /// Handles clicks for all dynamically generated shortcut buttons.
        /// </summary>
        private void QuickShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open the link.\n\nError: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retrieves and sets the current user's full display name from Active Directory.
        /// </summary>
        private void SetUserDisplayName()
        {
            try
            {
                UserNameValue.Text = UserPrincipal.Current.DisplayName;
            }
            catch (Exception)
            {
                // Fallback to the simple user name if the full name can't be retrieved
                UserNameValue.Text = Environment.UserName;
            }
        }


        /// <summary>
        /// Toggles the visibility of the popup window. This is the missing method.
        /// </summary>
        public void ToggleVisibility()
        {
            if (this.IsVisible)
            {
                this.Hide();
            }
            else
            {
                PositionNearTray();
                this.Show();
                this.Activate();
            }
        }

        /// <summary>
        /// This event handler is called whenever the popup window is shown or hidden.
        /// </summary>
        private async void PopupWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true && Application.Current is App app)
            {
                // --- Re-apply config in case it changed ---
                ApplyConfiguration();
                // ---

                // Refresh data when the window is opened
                await app.FetchAndSetIpAddressAsync();
                await app.FetchSnipeItDataAsync();

                // Set the tooltip for the risk score
                if (app.LastRiskScoreUpdate.HasValue)
                {
                    RiskScoreBorder.ToolTip = $"Last updated: {app.LastRiskScoreUpdate.Value:g}";
                }
                else
                {
                    RiskScoreBorder.ToolTip = "Not updated yet.";
                }

                UpdateRiskScoreTimestamp();

                // Start the live uptime timer and update it immediately
                UpdateUptimeText();
                _uptimeTimer.Start();
            }
            else
            {
                // Stop the timer when the window is hidden to save resources
                _uptimeTimer.Stop();
            }
        }

        /// <summary>
        /// Handles the timer's tick event to update the uptime display every second.
        /// </summary>
        private void UptimeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUptimeText();
        }

        /// <summary>
        /// Calculates and formats the system uptime and updates the UI.
        /// </summary>
        private void UpdateUptimeText()
        {
            if (UptimePanel.Visibility == Visibility.Collapsed)
            {
                return; // Do not calculate if panel is hidden
            }

            // Get the system uptime from the environment ticks
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            // Format the timespan to show days, hours, minutes, and seconds
            UptimeValue.Text = $"{(int)uptime.TotalDays}:{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
        }

        /// <summary>
        /// A reusable method to update the risk score's tooltip with the latest timestamp.
        /// </summary>
        private void UpdateRiskScoreTooltip()
        {
            if (Application.Current is App app)
            {
                if (app.LastRiskScoreUpdate.HasValue)
                {
                    RiskScoreBorder.ToolTip = $"Last updated: {app.LastRiskScoreUpdate.Value:g}";
                }
                else
                {
                    RiskScoreBorder.ToolTip = "Not updated yet.";
                }
            }
        }

        /// <summary>
        /// Calculates a human-readable relative time and updates the RiskScore timestamp display.
        /// </summary>
        private void UpdateRiskScoreTimestamp()
        {
            if (Application.Current is not App app || !app.LastRiskScoreUpdate.HasValue)
            {
                RiskScoreTimestamp.Text = string.Empty;
                return;
            }

            var timeSinceUpdate = DateTime.Now - app.LastRiskScoreUpdate.Value;

            string relativeTime;
            if (timeSinceUpdate.TotalSeconds < 60)
            {
                relativeTime = "just now";
            }
            else if (timeSinceUpdate.TotalMinutes < 60)
            {
                int minutes = (int)timeSinceUpdate.TotalMinutes;
                relativeTime = $"{minutes} minute{(minutes > 1 ? "s" : "")} ago";
            }
            else if (timeSinceUpdate.TotalHours < 24)
            {
                int hours = (int)timeSinceUpdate.TotalHours;
                relativeTime = $"{hours} hour{(hours > 1 ? "s" : "")} ago";
            }
            else
            {
                int days = (int)timeSinceUpdate.TotalDays;
                relativeTime = $"{days} day{(days > 1 ? "s" : "")} ago";
            }

            RiskScoreTimestamp.Text = $"({relativeTime})";
        }

        /// <summary>
        /// Positions the popup just above the system tray (bottom-right).
        /// </summary>
        public void PositionNearTray()
        {
            var workArea = SystemParameters.WorkArea; // usable screen without taskbar

            // Set to bottom-right
            this.Left = workArea.Right - this.Width - 10; // 10px margin
            this.Top = workArea.Bottom - this.Height - 10; // 10px margin
        }

        /// <summary>
        /// Copies the text from the TextBlock inside the clicked Border to the clipboard.
        /// </summary>
        private async void CopyValue_Click(object sender, MouseButtonEventArgs e)
        {
            // The sender is the Border, and its Child is the TextBlock
            if (sender is Border border && border.Child is TextBlock textBlock)
            {
                // Temporarily store the original text
                var originalText = textBlock.Text;

                if (!string.IsNullOrEmpty(originalText))
                {
                    // Copy to clipboard
                    Clipboard.SetText(originalText);

                    // Provide visual feedback by changing the text
                    textBlock.Text = "Copied!";
                    await Task.Delay(1500); // Wait for 1.5 seconds

                    // Restore the original text
                    textBlock.Text = originalText;
                }
            }
        }

        /// <summary>
        /// Fetches data from a JSON endpoint and updates the risk score value.
        /// </summary>
        private async void UpdateValue_Click(object sender, MouseButtonEventArgs e)
        {
            if (Application.Current is App app)
            {
                await app.FetchAndSetRiskScoreAsync();
                // After the update is complete, refresh the tooltip immediately.
                UpdateRiskScoreTooltip();
                UpdateRiskScoreTimestamp();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Set the placement target to the button itself
                button.ContextMenu.PlacementTarget = button;
                // Set the placement mode to open above the button
                button.ContextMenu.Placement = PlacementMode.Top;
                // Open the context menu
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Fired when the settings context menu is about to open.
        /// Used to dynamically show/hide menu items based on settings.
        /// </summary>
        private void ContextMenu_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Read the setting from ConfigManager
                bool isReminderEnabled = ConfigManager.GetBool("/Settings/ReminderOverlay/Enabled", true);

                // Set the visibility of the "Test Reminder" menu item
                TestReminderMenuItem.Visibility = isReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PopupWindow.ContextMenu_Loaded] Error updating menu item visibility: {ex.Message}");
                // Default to visible if something goes wrong to allow testing
                TestReminderMenuItem.Visibility = Visibility.Visible;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Check if window is already open
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
            }
            else
            {
                // Create new window
                _settingsWindow = new SettingsWindow();

                // Set owner to the main window (if it exists) or this popup
                _settingsWindow.Owner = Application.Current.MainWindow ?? this;

                // When closed, set our reference to null
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;

                _settingsWindow.Show();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open about window
            MessageBox.Show("Yonathan was here.");
        }

        /// <summary>
        /// Manually triggers the restart reminder overlay for testing.
        /// </summary>
        private void TestReminder_Click(object sender, RoutedEventArgs e)
        {
            // Call the public method on the main App class
            (Application.Current as App)?.ShowReminderOverlay();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Check if Ctrl, Shift, and Alt are ALL held down
            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (isCtrlPressed && isShiftPressed && isAltPressed)
            {
                Application.Current.Shutdown();
            }
        }

    }
}
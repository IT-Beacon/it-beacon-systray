using it_beacon_systray;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for ReminderOverlayWindow.xaml
    /// </summary>
    public partial class ReminderOverlayWindow : Window
    {
        // Public property to bind the uptime string to the TextBlock
        public string CurrentUptime { get; set; }

        private readonly int _deferenceCount;
        private readonly App? _mainApp;

        private readonly DispatcherTimer _uptimeTimer;

        private bool _allowClose = false; // Flag to allow closing only from buttons

        public ReminderOverlayWindow(int deferenceCount) // Removed uptimeDisplay parameter
        {
            InitializeComponent();

            _deferenceCount = deferenceCount;
            _mainApp = Application.Current as App;

            // Set deference UI (counter, button state)
            UpdateDeferenceUI();

            // --- INITIALIZE AND START DYNAMIC TIMER ---
            _uptimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uptimeTimer.Tick += UptimeTimer_Tick;

            // Update the text immediately on load
            UpdateUptimeText();
            // Start the timer
            _uptimeTimer.Start();

            // Hook into the Closed event to stop the timer
            this.Closed += OnWindowClosed;
        }

        /// <summary>
        /// Stops the timer when the window is closed to save resources.
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _uptimeTimer?.Stop();
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
            // Get the system uptime from the environment ticks
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            // Format the timespan to show days, hours, minutes, and seconds
            string uptimeString = $"{(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";

            // Set the text directly
            UptimeDisplay.Text = $"System Uptime: {uptimeString}";
        }

        /// <summary>
        /// Updates the UI elements based on the current deference count.
        /// </summary>
        private void UpdateDeferenceUI()
        {
            DeferenceCounterText.Text = $"Deferrals used: {_deferenceCount} / 3";

            if (_deferenceCount >= 3)
            {
                RestartLaterButton.Content = "Restart Required";
                RestartLaterButton.IsEnabled = false; // Disable the button
                RestartLaterButton.ToolTip = "Maximum deferrals reached. Please restart.";
            }
            else
            {
                RestartLaterButton.ToolTip = "Postpone the reminder for 6 hours.";
            }
        }
        // ---

        private void RestartNowButton_Click(object sender, RoutedEventArgs e)
        {

            _allowClose = true; // Signal that this is a permitted close

            try
            {
                // Start the shutdown process with a restart command
                Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                // Close the app itself
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initiate restart: {ex.Message}", "Error");
            }
        }

        private void RestartLaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainApp == null)
            {
                Debug.WriteLine("[ReminderOverlay] _mainApp was null. Deference not registered.");
                _allowClose = true; // Allow closing even if app reference is lost
                this.Close();
                return;
            }

            // Check if Left or Right Shift key is pressed
            bool isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Tell the main App to register the deference, passing the reset flag
            _mainApp.RegisterDeference(isReset: isShiftHeld);

            _allowClose = true; // Signal that this is a permitted close

            // Just close the reminder window
            this.Close();
        }

        /// <summary>
        /// Prevents the window from closing via Alt+F4 or other non-button methods.
        /// </summary>
        private void ReminderOverlayWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_allowClose)
            {
                // If the close was not triggered by our buttons, block it.
                e.Cancel = true;
            }
        }
    }
}
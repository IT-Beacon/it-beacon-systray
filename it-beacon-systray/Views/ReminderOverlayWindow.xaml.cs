using it_beacon_common.Config;
using it_beacon_systray.Models;
using it_beacon_systray.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for ReminderOverlayWindow.xaml
    /// </summary>
    public partial class ReminderOverlayWindow : Window
    {
        private readonly DispatcherTimer _uptimeTimer;
        private bool _allowClose = false;
        private readonly ReminderSettings _settings;
        private readonly int _deferenceCount;
        private readonly App? _mainApp;

        public ReminderOverlayWindow(int deferenceCount, string reminderMessage, ReminderSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _deferenceCount = deferenceCount;
            _mainApp = Application.Current as App;

            // Set initial UI text
            ReminderMessageText.Text = reminderMessage;
            DeferenceCounterText.Text = $"Deferrals used: {_deferenceCount} / {_settings.MaxDeferrals}";

            // Set button content from settings
            RestartNowButton.Content = _settings.PrimaryButtonText;
            RestartLaterButton.Content = _settings.DeferralButtonText;

            // Handle deferral limit
            if (_deferenceCount >= _settings.MaxDeferrals)
            {
                RestartLaterButton.IsEnabled = false;
                RestartLaterButton.ToolTip = "Maximum deferrals reached. Please restart.";
            }
            else
            {
                RestartLaterButton.IsEnabled = true;
                RestartLaterButton.ToolTip = $"Postpone the reminder for {_settings.DeferralDuration} minutes.";
            }

            // Set up and start the uptime timer
            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeTimer.Tick += UptimeTimer_Tick;
            _uptimeTimer.Start();
            UpdateUptime(); // Initial update

            Closed += OnWindowClosed;
        }

        private void UptimeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUptime();
        }

        private void UpdateUptime()
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeDisplay.Text = $"System Uptime: {(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _uptimeTimer?.Stop();
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

        private void RestartLaterButton_Click(object sender, RoutedEventArgs e)
        {
            bool isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            _mainApp?.RegisterDeference(isReset: isShiftHeld);
            _allowClose = true;
            this.Close();
        }

        private void RestartNowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initiate restart: {ex.Message}", "Error");
            }
            _allowClose = true;
            this.Close();
        }
    }
}
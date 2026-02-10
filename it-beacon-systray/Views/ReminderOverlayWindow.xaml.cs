using it_beacon_systray.Helpers;
using it_beacon_systray.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

            // Set all UI elements from the settings object
            this.Title = _settings.Title;
            HeaderTitle.Text = _settings.Title;
            HeaderIcon.Text = ((char)int.Parse(_settings.Glyph, NumberStyles.HexNumber)).ToString();
            
            // Use the formatter for the message
            TextBlockFormatter.SetFormattedText(ReminderMessageText, reminderMessage);

            DeferenceCounterText.Text = $"Deferrals used: {_deferenceCount} / {_settings.MaxDeferrals}";
            PrimaryButton.Content = _settings.PrimaryButtonText;
            DeferralButton.Content = _settings.DeferralButtonText;

            // Configure Header Image
            if (_settings.ShowHeaderImage)
            {
                HeroBannerBorder.Visibility = Visibility.Visible;
                try
                {
                    if (!string.IsNullOrEmpty(_settings.HeaderImageSource))
                    {
                        var brush = new ImageBrush();
                        brush.ImageSource = new BitmapImage(new Uri(_settings.HeaderImageSource, UriKind.RelativeOrAbsolute));
                        brush.Stretch = Stretch.UniformToFill;
                        HeroBannerBorder.Background = brush;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ReminderOverlayWindow] Failed to load header image: {ex.Message}");
                }
            }
            else
            {
                HeroBannerBorder.Visibility = Visibility.Collapsed;
            }

            // Handle deferral limit
            if (_deferenceCount >= _settings.MaxDeferrals)
            {
                DeferralButton.IsEnabled = false;
                DeferralButton.ToolTip = "Maximum deferrals reached. Please restart.";
            }
            else
            {
                DeferralButton.IsEnabled = true;
                DeferralButton.ToolTip = $"Postpone the reminder for {GetFriendlyDuration(_settings.DeferralDuration)}.";
            }

            // Set up and start the uptime timer
            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeTimer.Tick += UptimeTimer_Tick;
            _uptimeTimer.Start();
            UpdateUptime(); // Initial update

            Closed += OnWindowClosed;
        }

        /// <summary>
        /// Converts a duration in minutes to a user-friendly string (e.g., "2 hours", "3 days").
        /// </summary>
        private string GetFriendlyDuration(int minutes)
        {
            var duration = TimeSpan.FromMinutes(minutes);

            if (duration.TotalDays >= 1 && duration.TotalDays % 1 == 0)
            {
                int days = (int)duration.TotalDays;
                return $"{days} day{(days > 1 ? "s" : "")}";
            }
            if (duration.TotalHours >= 1 && duration.TotalHours % 1 == 0)
            {
                int hours = (int)duration.TotalHours;
                return $"{hours} hour{(hours > 1 ? "s" : "")}";
            }
            
            return $"{minutes} minute{(minutes > 1 ? "s" : "")}";
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

        private void DeferralButton_Click(object sender, RoutedEventArgs e)
        {
            bool isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            _mainApp?.RegisterDeference(isReset: isShiftHeld);
            _allowClose = true;
            this.Close();
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
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
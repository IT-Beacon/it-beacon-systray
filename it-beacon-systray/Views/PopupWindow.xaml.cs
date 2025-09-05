using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.DirectoryServices.AccountManagement; // Added for retrieving user display name
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    ///

    public partial class PopupWindow : Window
    {
        private readonly DispatcherTimer _uptimeTimer;
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

            // Initialize the timer for the live uptime counter
            _uptimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uptimeTimer.Tick += UptimeTimer_Tick;
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
                // Refresh data when the window is opened
                await app.FetchAndSetIpAddressAsync();

                // Set the tooltip for the risk score
                if (app.LastRiskScoreUpdate.HasValue)
                {
                    RiskScoreBorder.ToolTip = $"Last updated: {app.LastRiskScoreUpdate.Value:g}";
                }
                else
                {
                    RiskScoreBorder.ToolTip = "Not updated yet.";
                }

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
        /// Positions the popup just above the system tray (bottom-right).
        /// </summary>
        public void PositionNearTray()
        {
            var workArea = SystemParameters.WorkArea; // usable screen without taskbar

            // Set to bottom-right
            this.Left = workArea.Right - this.Width - 10; // 10px margin
            this.Top = workArea.Bottom - this.Height - 10; // 10px margin
        }

        // --- NEW EVENT HANDLERS ---

        private void CompanyPortal_Click(object sender, RoutedEventArgs e)
        {
            // Add logic to open Company Portal here
            MessageBox.Show("Company Portal button clicked!");
        }

        private void Terminal_Click(object sender, RoutedEventArgs e)
        {
            // Add logic to open Windows Terminal here
            MessageBox.Show("Terminal button clicked!");
        }

        private void Homepage_Click(object sender, RoutedEventArgs e)
        {
            // Add logic to open the UNT CVAD IT Homepage here
            MessageBox.Show("Homepage button clicked!");
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
            }
        }

        
    }
}

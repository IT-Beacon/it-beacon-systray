using System.Windows;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System;

namespace it_beacon_systray.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow(string message, string repoUrl)
        {
            InitializeComponent();
            InfoText.Text = message;

            if (!string.IsNullOrEmpty(repoUrl) && Uri.TryCreate(repoUrl, UriKind.Absolute, out var uri))
            {
                RepoLink.NavigateUri = uri;
                RepoLinkText.Text = repoUrl;
            }
            else
            {
                RepoLinkText.Text = "Unknown Repository";
                RepoLink.IsEnabled = false;
            }

            // Apply Immersive Dark Mode to title bar
            this.SourceInitialized += (s, e) => ApplyDarkMode();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private void ApplyDarkMode()
        {
            try
            {
                bool isDark = false;
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int i)
                    {
                         isDark = (i == 0);
                    }
                }

                if (isDark)
                {
                    var windowHelper = new WindowInteropHelper(this);
                    int attribute = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE
                    int useDarkMode = 1;
                    DwmSetWindowAttribute(windowHelper.Handle, attribute, ref useDarkMode, sizeof(int));
                }
            }
            catch { }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    }
}
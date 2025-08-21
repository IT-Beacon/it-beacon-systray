using Hardcodet.Wpf.TaskbarNotification;
using it_beacon_systray.Helpers;
using it_beacon_systray.Views;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace it_beacon_systray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private PopupWindow? _popupWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply theme once at startup
            ThemeHelper.ApplyTheme();

            // Start listening for future changes
            ThemeHelper.ListenForThemeChanges();

            // preload popup window
            _popupWindow = new PopupWindow();

            // load tray icon, wire events, etc...

            // Preload popup window
            _popupWindow = new PopupWindow();

            // preload tray icon resource
            var trayIcon = (TaskbarIcon)FindResource("TrayIcon");

            // load embedded icon from it-beacon-common
            var iconUri = new Uri("pack://application:,,,/it-beacon-common;component/Assets/Icons/beacon.ico");
            trayIcon.IconSource = new BitmapImage(iconUri);

            _notifyIcon = trayIcon;

            // wire up events
            _notifyIcon.TrayLeftMouseUp += (s, ev) => TogglePopup();
            _notifyIcon.TrayRightMouseUp += (s, ev) => TogglePopup();
            _notifyIcon.TrayMouseDoubleClick += (s, ev) => OpenNotepad();
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

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

        private void ApplyTheme()
        {
            bool isLight = ThemeHelper.IsLightTheme;

            Resources["PopupBackgroundBrush"] = isLight
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48));

            Resources["PopupForegroundBrush"] = isLight
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);

            Resources["PopupBorderBrush"] = isLight
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
        }

    }

}

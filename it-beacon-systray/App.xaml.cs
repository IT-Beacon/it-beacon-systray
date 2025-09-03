using Hardcodet.Wpf.TaskbarNotification;
using it_beacon_common.Helpers; // Correctly using the common library's helper
using it_beacon_systray.Views;
using System.Diagnostics;
using System.Windows;
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

            // Apply theme once at startup from the common library
            ThemeHelper.ApplyTheme();

            // Start listening for future changes
            ThemeHelper.RegisterThemeChangeListener();

            // Preload popup window
            _popupWindow = new PopupWindow();

            // Preload tray icon resource
            var trayIcon = (TaskbarIcon)FindResource("TrayIcon");

            // Load embedded icon from it-beacon-common
            var iconUri = new Uri("pack://application:,,,/it-beacon-common;component/Assets/Icons/beacon.ico");
            trayIcon.IconSource = new BitmapImage(iconUri);
            _notifyIcon = trayIcon;

            // Wire up events
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
    }
}
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using it_beacon_systray_app.Helpers;

namespace it_beacon_systray_app
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize the tray icon
            _trayIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(
                    Application.GetResourceStream(
                        new Uri("pack://application:,,,/it-beacon-common;component/Assets/Icons/beacon.ico")
                    ).Stream
                ),
                ToolTipText = "IT Beacon"
            };

            // Handle left/right clicks
            _trayIcon.TrayLeftMouseUp += (s, args) => ShowRichPopup();
            _trayIcon.TrayRightMouseUp += (s, args) => ShowRichPopup();
        }

        private void ShowRichPopup()
        {
            // If already open, bring it forward
            foreach (Window win in Current.Windows)
            {
                if (win is TrayPopupWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }

            var popup = new TrayPopupWindow();

            // Position popup dynamically
            PopupHelper.PositionPopup(popup);

            popup.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : Window
    {
        public PopupWindow()
        {
            InitializeComponent();

            // Close popup when it loses focus
            this.Deactivated += (s, e) => this.Hide();
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
    }
}

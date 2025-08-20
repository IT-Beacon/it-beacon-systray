using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows; // for Window and System.Windows.Point
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point; // alias to avoid ambiguity

namespace it_beacon_systray_app.Helpers
{
    public static class PopupHelper
    {
        /// <summary>
        /// Positions the popup on the monitor where the mouse cursor currently is,
        /// aligned to bottom-right (like system tray behavior).
        /// </summary>
        public static void PositionPopup(Window popup)
        {
            popup.Loaded += (s, e) =>
            {
                // Get monitor where mouse is
                var screen = Screen.FromPoint(System.Windows.Forms.Control.MousePosition);

                var workingArea = screen.WorkingArea;

                double left = workingArea.Right - popup.ActualWidth - 10;
                double top = workingArea.Bottom - popup.ActualHeight - 10;

                popup.Left = left;
                popup.Top = top;

                System.Diagnostics.Debug.WriteLine(
                    $"Placing popup on screen {screen.DeviceName}: Left={left}, Top={top}, Width={popup.ActualWidth}, Height={popup.ActualHeight}"
                );
            };
        }
    }
}

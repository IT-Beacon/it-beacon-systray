using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media;

namespace it_beacon_systray.Helpers
{
    public static class ThemeHelper
    {
        public static bool IsLightTheme => GetSystemTheme();

        public static event Action? ThemeChanged;

        public static Color Lighten(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                (byte)Math.Min(255, color.B + (255 - color.B) * factor)
            );
        }

        public static Color Darken(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        /// <summary>
        /// Apply current theme to app resources (brushes, etc.)
        /// </summary>
        public static void ApplyTheme()
        {
            bool useLightTheme = IsLightTheme;

            if (useLightTheme)
            {
                Application.Current.Resources["PopupBackgroundBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["PopupForegroundBrush"] = new SolidColorBrush(Colors.Black);
                Application.Current.Resources["PopupBorderBrush"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                Application.Current.Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                Application.Current.Resources["ButtonForegroundBrush"] = new SolidColorBrush(Colors.Black);
                Application.Current.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(208, 208, 208));
            }
            else
            {
                Application.Current.Resources["PopupBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                Application.Current.Resources["PopupForegroundBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["PopupBorderBrush"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                Application.Current.Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Application.Current.Resources["ButtonForegroundBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }

            var accent = SystemParameters.WindowGlassColor;

            var resources = Application.Current.Resources;
            resources["AccentBrush"] = new SolidColorBrush(accent);
            resources["AccentHoverBrush"] = new SolidColorBrush(Lighten(accent, 0.25)); // 25% lighter
            resources["AccentPressedBrush"] = new SolidColorBrush(Darken(accent, 0.2)); // 20% darker
        }

        /// <summary>
        /// Start listening to Windows theme changes
        /// </summary>
        public static void ListenForThemeChanges()
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General ||
                e.Category == UserPreferenceCategory.Color)
            {
                ApplyTheme(); // update immediately
                ThemeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Reads Windows registry for current theme preference
        /// </summary>
        private static bool GetSystemTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value is int intVal)
                    {
                        return intVal > 0;
                    }
                }
            }
            catch { }

            // Default to Light if unknown
            return true;
        }
    }
}

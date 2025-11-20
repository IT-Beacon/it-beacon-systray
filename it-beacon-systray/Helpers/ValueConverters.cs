using System;
using System.Globalization;
using System.Windows.Data;

namespace it_beacon_systray.Helpers
{
    public class StringToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && bool.TryParse(s, out bool result))
            {
                return result;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b.ToString().ToLowerInvariant();
            }
            return "false";
        }
    }

    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return false;
        }
    }

    public class StringToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string glyphString && !string.IsNullOrEmpty(glyphString))
            {
                // The string is expected to be in the format "&#xABCD;"
                // We need to extract the hex part "ABCD" and convert it to a char.
                try
                {
                    string hex = glyphString.Trim(new[] { '&', '#', 'x', ';' });
                    int intValue = int.Parse(hex, NumberStyles.HexNumber);
                    return (char)intValue;
                }
                catch
                {
                    // Return empty if parsing fails
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for this use case
            throw new NotImplementedException();
        }
    }
}
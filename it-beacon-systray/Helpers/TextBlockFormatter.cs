using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace it_beacon_systray.Helpers
{
    public static class TextBlockFormatter
    {
        public static readonly DependencyProperty FormattedTextProperty =
            DependencyProperty.RegisterAttached(
                "FormattedText",
                typeof(string),
                typeof(TextBlockFormatter),
                new PropertyMetadata(string.Empty, OnFormattedTextChanged));

        public static string GetFormattedText(DependencyObject obj)
        {
            return (string)obj.GetValue(FormattedTextProperty);
        }

        public static void SetFormattedText(DependencyObject obj, string value)
        {
            obj.SetValue(FormattedTextProperty, value);
        }

        private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContentControl contentControl)
            {
                var formattedText = e.NewValue as string;
                if (!string.IsNullOrEmpty(formattedText))
                {
                    // Wrap the string in a TextBlock to handle parsing of Inlines like <Bold>
                    var fullXaml = $@"<TextBlock xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TextWrapping=""Wrap"">{formattedText}</TextBlock>";
                    try
                    {
                        var content = XamlReader.Parse(fullXaml);
                        contentControl.Content = content;
                    }
                    catch (XamlParseException)
                    {
                        // In case of parsing error, fall back to plain text
                        contentControl.Content = new TextBlock { Text = formattedText, TextWrapping = TextWrapping.Wrap };
                    }
                }
                else
                {
                    contentControl.Content = null;
                }
            }
        }
    }
}
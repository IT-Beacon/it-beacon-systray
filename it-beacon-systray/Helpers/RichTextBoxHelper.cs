using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace it_beacon_systray.Helpers
{
    public static class RichTextBoxHelper
    {
        public static readonly DependencyProperty DocumentXamlProperty =
            DependencyProperty.RegisterAttached(
                "DocumentXaml",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.None,
                    OnDocumentXamlChanged));

        public static string GetDocumentXaml(DependencyObject obj)
        {
            return (string)obj.GetValue(DocumentXamlProperty);
        }

        public static void SetDocumentXaml(DependencyObject obj, string value)
        {
            obj.SetValue(DocumentXamlProperty, value);
        }

        private static void OnDocumentXamlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Forward to the new handler
            OnDocumentSourceChanged(d, e);
        }

        public static readonly DependencyProperty DocumentSourceProperty =
            DependencyProperty.RegisterAttached(
            "DocumentSource",
            typeof(object),
            typeof(RichTextBoxHelper),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.None,
                OnDocumentSourceChanged));

        public static object GetDocumentSource(DependencyObject obj)
        {
            return obj.GetValue(DocumentSourceProperty);
        }

        public static void SetDocumentSource(DependencyObject obj, object value)
        {
            obj.SetValue(DocumentSourceProperty, value);
        }

        private static void OnDocumentSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox rtb)
            {
                rtb.Document = new FlowDocument(); // Clear existing content

                if (e.NewValue is FlowDocument flowDoc)
                {
                    rtb.Document = flowDoc;
                }
                else if (e.NewValue is string xamlText && !string.IsNullOrEmpty(xamlText))
                {
                    // Wrap the content in a FlowDocument
                    var fullXaml = $"<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{xamlText}</FlowDocument>";

                    try
                    {
                        var newFlowDoc = (FlowDocument)XamlReader.Parse(fullXaml);
                        rtb.Document = newFlowDoc;
                    }
                    catch (XamlParseException)
                    {
                        // Handle cases where the XAML is invalid by leaving the document empty
                    }
                }
            }
        }
    }
}

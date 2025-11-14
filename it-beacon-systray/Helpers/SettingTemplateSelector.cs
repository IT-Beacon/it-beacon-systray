using System.Windows;
using System.Windows.Controls;

namespace it_beacon_systray.Helpers
{
    public class SettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? CheckBoxTemplate { get; set; }
        public DataTemplate? MultiLineTextTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item != null)
            {
                var keyProperty = item.GetType().GetProperty("Key");
                if (keyProperty?.GetValue(item)?.ToString() == "Reminder Message")
                {
                    return MultiLineTextTemplate!;
                }

                var valueProperty = item.GetType().GetProperty("Value");
                if (valueProperty != null)
                {
                    var value = valueProperty.GetValue(item)?.ToString();
                    if (bool.TryParse(value, out _))
                    {
                        return CheckBoxTemplate!;
                    }
                }
            }
            return TextTemplate!;
        }
    }
}
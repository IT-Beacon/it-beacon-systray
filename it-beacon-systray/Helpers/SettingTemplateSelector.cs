using it_beacon_common.Config;
using System.Windows;
using System.Windows.Controls;
using it_beacon_systray.Models; // Add this using directive for the SettingItem class

namespace it_beacon_systray.Helpers
{
    public class SettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? CheckBoxTemplate { get; set; }
        public DataTemplate? MultiLineTextTemplate { get; set; }
        public DataTemplate? GlyphTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is SettingItem setting)
            {
                return setting.IsType switch
                {
                    "bool" => CheckBoxTemplate!,
                    "multiline" => MultiLineTextTemplate!,
                    "glyph" => GlyphTemplate!,
                    _ => TextTemplate!,
                };
            }
            return TextTemplate!;
        }
    }
}
using it_beacon_common.Config;
using it_beacon_systray.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace it_beacon_systray.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // Full list of all settings
        private readonly ObservableCollection<SettingItem> _allSettings;

        // List of distinct categories for the left panel
        public ObservableCollection<string> Categories { get; set; }

        // Filtered view of settings for the right panel
        public ICollectionView FilteredSettings { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();

            // Display the configuration file path
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "settings.xml");
            if (File.Exists(configFilePath))
            {
                ConfigPathLabel.Text = $"Loaded from: {configFilePath}";
            }
            else
            {
                ConfigPathLabel.Text = $"Configuration file not found. Please ensure 'settings.xml' exists at: {configFilePath}";
            }

            // Load all settings from the ConfigManager
            _allSettings = new ObservableCollection<SettingItem>(ConfigManager.GetAllSettings());

            // Get app name and version from the assembly
            var assembly = Assembly.GetExecutingAssembly();
            var appName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "IT Beacon";
            var appVersion = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            // Set the version label text
            VersionLabel.Text = $"{appName} v{appVersion}";

            // Create the list of categories from the settings, excluding "Application"
            Categories = new ObservableCollection<string>(
                _allSettings.Select(s => s.Category)
                            .Distinct()
                            .Where(c => c != "Application")
                            .OrderBy(c => c)
            );

            // Create the filtered collection view
            FilteredSettings = CollectionViewSource.GetDefaultView(
                _allSettings.Where(s => s.Category != "Application")
            );
            FilteredSettings.Filter = FilterSettings;

            // Set the DataContext to this window instance itself for binding
            this.DataContext = this;

            // Set default selection
            if (CategoryListBox.Items.Count > 0)
            {
                CategoryListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Called when the user selects a new category from the ListBox.
        /// </summary>
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Refresh the filter whenever the selection changes
            FilteredSettings.Refresh();

            if (CategoryListBox.SelectedItem is string selectedCategory)
            {
                SettingsHeader.Text = $"{selectedCategory} Settings";
            }
        }

        /// <summary>
        /// Saves all changes and closes the window.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.SaveAllSettings(_allSettings);
            DialogResult = true;
        }

        /// <summary>
        /// Closes the window without saving any pending changes.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Saves all pending changes to the settings.xml file.
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.SaveAllSettings(_allSettings);
        }

        /// <summary>
        /// The filter logic for the ICollectionView.
        /// </summary>
        private bool FilterSettings(object item)
        {
            if (item is not SettingItem setting)
            {
                return false;
            }

            // If no category is selected, don't show anything
            if (CategoryListBox.SelectedItem is not string selectedCategory)
            {
                return false;
            }

            // Show items that match the selected category
            return setting.Category == selectedCategory;
        }

        /// <summary>
        /// Handles the KeyDown event for the multi-line TextBox to ensure Enter creates a new line.
        /// </summary>
        private void MultiLineTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var textBox = (TextBox)sender;
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }
        }

        // --- NEW CONVERTER CLASS ---
        /// <summary>
        /// Converts a string ("true", "false") to a bool for CheckBox binding.
        /// </summary>
        public class BooleanToStringConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                // Convert string "true" (any case) to bool true
                return (value as string)?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                // Convert bool true back to string "true"
                return (value is bool b && b) ? "true" : "false";
            }
        }
    }
}
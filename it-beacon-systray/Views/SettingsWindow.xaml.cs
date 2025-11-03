using it_beacon_common.Config;
using System; // Required for IValueConverter
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization; // Required for IValueConverter
using System.Linq;
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

            // Load all settings from the ConfigManager
            _allSettings = new ObservableCollection<SettingItem>(ConfigManager.GetAllSettings());

            // Create the list of categories from the settings
            Categories = new ObservableCollection<string>(
                _allSettings.Select(s => s.Category).Distinct().OrderBy(c => c)
            );

            // "All" category is a good default
            Categories.Insert(0, "All");

            // Create the filtered collection view
            FilteredSettings = CollectionViewSource.GetDefaultView(_allSettings);
            FilteredSettings.Filter = FilterSettings;

            // Set the DataContext to this window instance itself for binding
            this.DataContext = this;

            // Set default selection
            CategoryListBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the user selects a new category from the ListBox.
        /// </summary>
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the header text
            string selectedCategory = CategoryListBox.SelectedItem as string ?? "All";
            SettingsHeader.Text = $"{selectedCategory} Settings";

            // Refresh the filter on the settings ListView
            FilteredSettings.Refresh();
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

            string selectedCategory = CategoryListBox.SelectedItem as string ?? "All";

            // If "All" is selected, show everything
            if (selectedCategory == "All")
            {
                return true;
            }

            // Otherwise, only show items that match the selected category
            return setting.Category == selectedCategory;
        }

        // --- NEW BUTTON HANDLERS ---

        /// <summary>
        /// Saves all changes and closes the window.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyButton_Click(sender, e);
            this.DialogResult = true; // Use DialogResult for modal windows
            this.Close();
        }

        /// <summary>
        /// Closes the window without saving any pending changes.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Changes are only in memory in the SettingItem objects.
            // Closing the window discards them.
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Saves all pending changes to the settings.xml file.
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.SaveAllSettings(_allSettings))
            {
                // Optionally, provide feedback
                // ApplyButton.Content = "Saved!"; 
            }
            else
            {
                MessageBox.Show("There was an error saving the settings. Please check the log.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
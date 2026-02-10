using it_beacon_common.Config;
using it_beacon_systray.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal; // Required for checking Admin privileges
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
        
        // Store the final resolved config path so it can be used in LockDownInterface
        private string _resolvedConfigPath;

        // List of distinct categories for the left panel
        public ObservableCollection<string> Categories { get; set; }

        // Filtered view of settings for the right panel
        public ICollectionView FilteredSettings { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();

            // Determine the configuration file path with priority:
            // 1. ProgramData (System-wide, secure)
            // 2. Local "config" folder (Runtime/Debug)
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IT-Beacon", "settings.xml");
            string localConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "settings.xml");

            if (File.Exists(programDataPath))
            {
                _resolvedConfigPath = programDataPath;
            }
            else
            {
                _resolvedConfigPath = localConfigPath;
            }

            // Display the path
            if (File.Exists(_resolvedConfigPath))
            {
                ConfigPathLabel.Text = $"Loaded from: {_resolvedConfigPath}";
            }
            else
            {
                ConfigPathLabel.Text = $"Configuration file not found. Please ensure 'settings.xml' exists at: {_resolvedConfigPath}";
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

            // --- BEST PRACTICE SECURITY: Lock down UI if not Admin ---
            if (!IsUserAdministrator())
            {
                LockDownInterface();
            }
            else
            {
                // If running as Admin, show the option to relaunch as standard user
                if (RevertAdminButton != null)
                {
                    RevertAdminButton.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// Checks if the current user process has Administrator privileges.
        /// </summary>
        private bool IsUserAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Disables editing capabilities for standard users.
        /// </summary>
        private void LockDownInterface()
        {
            // 1. Update Visuals to indicate Read-Only mode
            this.Title += " (Read Only - Administrator Rights Required)";
            SettingsHeader.Text += " (Read Only)";
            
            // Set the ToolTip to the actual path using the resolved variable
            ConfigPathLabel.ToolTip = _resolvedConfigPath;

            ConfigPathLabel.Text = "Standard users cannot modify these settings.";
            ConfigPathLabel.Foreground = System.Windows.Media.Brushes.OrangeRed;

            // 2. Disable OK button
            OkButton.IsEnabled = false;
            OkButton.ToolTip = "Administrator rights are required to save changes.";

            // 3. Repurpose Apply button to allow Relaunching as Admin
            ApplyButton.Content = "Relaunch as Admin";
            ApplyButton.IsEnabled = true;
            ApplyButton.ToolTip = "Restart application with Administrator privileges to edit settings.";

            // 4. Lock individual input fields
            foreach (var setting in _allSettings)
            {
                setting.IsReadOnly = true;
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
                
                // Re-append read-only warning if necessary
                if (!OkButton.IsEnabled) 
                {
                    SettingsHeader.Text += " (Read Only)";
                }
            }
        }

        /// <summary>
        /// Relaunches the application as the current logged-on user (Standard User).
        /// Uses explorer.exe to trigger the launch, as Explorer runs with user privileges.
        /// </summary>
        private void RevertAdminButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? exeName = Process.GetCurrentProcess().MainModule?.FileName;
                if (exeName != null)
                {
                    // Launching via Explorer de-elevates the process
                    Process.Start("explorer.exe", exeName);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to relaunch as standard user: {ex.Message}", "Relaunch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
        /// Saves all pending changes to the settings.xml file or Relaunches as Admin.
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if we are in "Relaunch" mode (set by LockDownInterface)
            if (ApplyButton.Content is string content && content == "Relaunch as Admin")
            {
                try
                {
                    string? exeName = Process.GetCurrentProcess().MainModule?.FileName;
                    if (exeName != null)
                    {
                        var startInfo = new ProcessStartInfo(exeName)
                        {
                            UseShellExecute = true,
                            Verb = "runas" // Triggers the UAC prompt for elevation
                        };
                        Process.Start(startInfo);
                        Application.Current.Shutdown(); // Close the current non-admin instance
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restart as Admin: {ex.Message}", "Elevation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // Normal 'Apply' behavior
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
    }
}
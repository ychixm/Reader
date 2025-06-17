using System.Collections.ObjectModel; // For ObservableCollection
using System.Windows; // For RoutedEventArgs
using System.Windows.Controls; // For UserControl, GroupBox, MessageBox
using Utils; // For ISubApplication, IOptionsViewModel

namespace Assistant
{
    public partial class OptionsUserControl : UserControl
    {
        // Collection to hold the GroupBox views (wrapping UserControl views from each sub-application's IOptionsViewModel)
        public ObservableCollection<GroupBox> OptionViews { get; set; }

        public OptionsUserControl()
        {
            InitializeComponent();
            OptionViews = new ObservableCollection<GroupBox>(); // Changed to GroupBox
            DataContext = this; // Set DataContext for potential bindings to OptionViews from XAML if needed,
                                // though ItemsSource will be set directly.

            LoadOptionViews();

            // Register the event handler for the Apply button
            // Ensure ApplyOptionsButton is accessible here (should have x:Name in XAML)
            if (ApplyOptionsButton != null)
            {
                ApplyOptionsButton.Click += ApplyOptionsButton_Click;
            }
            else
            {
                // This indicates an issue, perhaps x:Name was missed or is different.
                MessageBox.Show("ApplyOptionsButton not found in OptionsUserControl.xaml!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOptionViews()
        {
            OptionViews.Clear();
            if (MainFrame.LoadedSubApplications != null)
            {
                foreach (var app in MainFrame.LoadedSubApplications)
                {
                    IOptionsViewModel? optionsViewModel = app.GetOptionsViewModel();
                    if (optionsViewModel != null)
                    {
                        UserControl? optionView = optionsViewModel.GetView();
                        if (optionView != null)
                        {
                            // Add a header or title for each options section
                            var groupBox = new GroupBox
                            {
                                Header = optionsViewModel.Title,
                                Content = optionView,
                                Margin = new Thickness(0, 0, 0, 10) // Add some spacing between option groups
                            };
                            OptionViews.Add(groupBox);
                        }
                    }
                }
            }

            // Set the ItemsSource for the ItemsControl defined in OptionsUserControl.xaml
            if (OptionsItemsControl != null)
            {
                OptionsItemsControl.ItemsSource = OptionViews;
            }
            else
            {
                MessageBox.Show("OptionsItemsControl not found in OptionsUserControl.xaml!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            bool allAppliedSuccessfully = true;
            if (MainFrame.LoadedSubApplications != null)
            {
                foreach (var app in MainFrame.LoadedSubApplications)
                {
                    try
                    {
                        IOptionsViewModel? optionsViewModel = app.GetOptionsViewModel();
                        optionsViewModel?.Apply(); // Call Apply on the ViewModel first

                        app.ApplyOptions(); // Then call ApplyOptions on the SubApplication
                    }
                    catch (System.Exception ex)
                    {
                        allAppliedSuccessfully = false;
                        MessageBox.Show($"Error applying options for {app.Name}:\n{ex.Message}", "Apply Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        // Optionally, break or continue based on desired error handling strategy
                    }
                }
            }

            if (allAppliedSuccessfully)
            {
                MessageBox.Show("Options applied!", "Confirmation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Some options were applied, but one or more errors occurred. Please check details.", "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

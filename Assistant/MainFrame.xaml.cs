using System.Collections.Generic; // For List
using System.Windows;
using System.Windows.Controls;    // For TabControl, TabItem
// using Reader.UserControls; // Not strictly needed now in C#
// using Reader.Business;   // Not directly used here
using Reader;            // For ReaderSubApplication
using SoundWeaver;
using Utils;             // For ISubApplication
// using Reader.Models;     // Not directly used here
using Utils.Models;      // Already there, for TabOverflowMode

// OptionsUserControl is in the same namespace (Assistant)

namespace Assistant
{
    public partial class MainFrame : Window
    {
        public TabOverflowMode CurrentTabOverflowMode { get; set; } // Existing property

        private List<ISubApplication> _subApplications = new List<ISubApplication>();
        public static List<ISubApplication> LoadedSubApplications { get; private set; } = new List<ISubApplication>();

        public MainFrame()
        {
            InitializeComponent();
            this.CurrentTabOverflowMode = TabOverflowMode.Scrollbar; // Existing line
            this.DataContext = this; // Existing line

            LoadSubApplications();
            InitializeTabsAndOptions(); // Renamed for clarity
        }

        private void LoadSubApplications()
        {
            var readerApp = new ReaderSubApplication();
            var SoundWeaverSubApp = new SoundWeaverSubApplication();
            _subApplications.Add(readerApp);
            _subApplications.Add(SoundWeaverSubApp);

            LoadedSubApplications.Clear();
            LoadedSubApplications.AddRange(_subApplications);
        }

        private void InitializeTabsAndOptions()
        {
            if (MainAppTabControl == null)
            {
                MessageBox.Show("Error: MainAppTabControl is not defined or named in MainFrame.xaml.",
                                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MainAppTabControl.Items.Clear(); // Clear any design-time tabs

            // Add the Options Tab
            TabItem optionsTab = new TabItem
            {
                Header = "Options",
                Content = new OptionsUserControl() // Create an instance of our OptionsUserControl
            };
            MainAppTabControl.Items.Add(optionsTab);

            // Add tabs for each sub-application's main view
            foreach (var app in _subApplications)
            {
                UserControl mainView = app.GetMainView();
                if (mainView != null)
                {
                    TabItem appTabItem = new TabItem
                    {
                        Header = app.Name,
                        Content = mainView
                    };
                    MainAppTabControl.Items.Add(appTabItem);
                }
                else
                {
                     MessageBox.Show($"Error: Main view for {app.Name} could not be loaded.",
                                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Existing MainFrame_Loaded event handler - keep as is
        private void MainFrame_Loaded(object sender, RoutedEventArgs e)
        {
            // It's possible that ThemeManager.ApplyTheme() might be called here
            // or ensured it's applied correctly if App.xaml.cs OnStartup is not sufficient
            // for dynamically loaded main windows. For now, we assume App.xaml.cs handles it.
            // ThemeManager.ApplyTheme(); // Re-evaluate if theming issues arise.
        }
    }
}

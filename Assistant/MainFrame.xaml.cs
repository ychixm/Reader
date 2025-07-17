using System.Collections.Generic; // For List
using System.Windows;
using System.Windows.Controls;    // For TabControl, TabItem
// using Reader.UserControls; // Not strictly needed now in C#
// using Reader.Business;   // Not directly used here
using Reader;            // For ReaderSubApplication
using SoundWeaver;
using Utils;             // For ISubApplication, ILoggerService
// using Reader.Models;     // Not directly used here
using Utils.Models;      // Already there, for TabOverflowMode
using Microsoft.Extensions.DependencyInjection; // For GetRequiredService

// OptionsUserControl is in the same namespace (Assistant)

namespace Assistant
{
    public partial class MainFrame : Window
    {
        private readonly ILoggerService _logger;
        public TabOverflowMode CurrentTabOverflowMode { get; set; } // Existing property

        private List<ISubApplication> _subApplications = new List<ISubApplication>();
        public static List<ISubApplication> LoadedSubApplications { get; private set; } = new List<ISubApplication>();

        public MainFrame() // This constructor is called by App.xaml.cs after ServiceProvider is built
        {
            InitializeComponent();

            // Resolve services from the static ServiceProvider in App
            _logger = App.ServiceProvider.GetRequiredService<ILoggerService>();

            _logger.LogInfo("MainFrame initializing...");

            this.CurrentTabOverflowMode = TabOverflowMode.Scrollbar; // Existing line
            this.DataContext = this; // Existing line

            LoadSubApplications();
            InitializeTabsAndOptions(); // Renamed for clarity
            _logger.LogInfo("MainFrame initialized.");
        }

        private void LoadSubApplications()
        {
            _logger.LogInfo("Loading sub-applications...");
            // Pass the logger to sub-application constructors
            var readerApp = new ReaderSubApplication(_logger);
            var soundWeaverSubApp = new SoundWeaverSubApplication(_logger);
            _subApplications.Add(readerApp);
            _subApplications.Add(soundWeaverSubApp);

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

using System.Windows;
using Reader.UserControls;
using Reader.Business;
using Reader;
using Utils;
using Reader.Models;
using Utils.Models; // Added using statement

namespace Assistant
{
    /// <summary>
    /// Interaction logic for MainFrame.xaml
    /// </summary>
    public partial class MainFrame : Window
    {
        public TabOverflowMode CurrentTabOverflowMode { get; set; } // Added property

        public MainFrame()
        {
            InitializeComponent();
            this.CurrentTabOverflowMode = TabOverflowMode.Scrollbar; // Added line
            this.DataContext = this; // Added line
        }

        private void MainFrame_Loaded(object sender, RoutedEventArgs e)
        {
            // It's possible that ThemeManager.ApplyTheme() might need to be called here
            // or ensured it's applied correctly if App.xaml.cs OnStartup is not sufficient
            // for dynamically loaded main windows. For now, we assume App.xaml.cs handles it.
            // ThemeManager.ApplyTheme(); // Re-evaluate if theming issues arise.
        }
    }
}

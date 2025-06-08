using System.Windows;
using Reader.Business; // For ThemeManager

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainFrame.xaml
    /// </summary>
    public partial class MainFrame : Window
    {
        public MainFrame()
        {
            InitializeComponent();
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

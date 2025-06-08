using System.Configuration;
using System.Data;
using System.Windows;
using Utils; // Added for ThemeManager

namespace Reader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e); // Call base implementation first

            ThemeManager.ApplyTheme(); // Apply the theme
        }
    }

}

using System.Configuration;
using System.Data;
using System.Windows;
using Reader.Business; // For ThemeManager, assuming it's in Reader.Business namespace

namespace Reader.MainHost
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // ThemeManager is in the Reader.Business namespace, which should be
            // accessible via the project reference from Reader.MainHost to Reader.
            ThemeManager.ApplyTheme();
        }
    }
}

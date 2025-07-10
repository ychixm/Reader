using System.Configuration;
using System.Data;
using System.Windows;

namespace SoundWeaver
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Making Dispatcher static for easy access from ViewModels or other non-UI classes
        // if needed, though direct use from VM is not ideal MVVM.
        // For simple cases like OnLayerEndedUpdateList, it's pragmatic.
        // public static new Dispatcher CurrentDispatcher { get; private set; } // Hides Application.Current.Dispatcher

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // CurrentDispatcher = App.Current.Dispatcher; // Assign if using the static property above
        }
    }
}

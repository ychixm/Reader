using System.Windows;

namespace SoundWeaver
{
    /// <summary>
    /// Interaction logic for the new MainWindow.xaml that hosts SoundWeaverControl.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Any specific logic for the host window itself can go here.
            // For example, if the SoundWeaverControl's ViewModel needed to be
            // instantiated or configured by its host, that could happen here,
            // though in the current setup, SoundWeaverControl instantiates its own ViewModel.
        }

        // If the main application window closing needs to trigger specific cleanup
        // for resources not handled by SoundWeaverControl's Unloaded event,
        // that could be done here. However, if SoundWeaverControl handles its own
        // ViewModel's disposal on Unload, this might not be strictly necessary
        // for the control itself.
        //
        // private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        // {
        //     // Example: if you need to access the control and call a specific cleanup method:
        //     // if (this.Content is SoundWeaverControl swControl)
        //     // {
        //     //     // Call any specific cleanup on swControl if needed
        //     // }
        // }
    }
}

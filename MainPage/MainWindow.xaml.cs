using System.Windows; // This using is important for 'Window'

namespace Reader
{
    /// <summary>
    /// Old MainWindow.xaml.cs. Functionality moved to MainUserControl.xaml.cs and MainFrame.xaml.
    /// This file is kept to prevent build errors from missing partial class, but it's not used.
    /// </summary>
    public partial class MainWindow : Window // Changed BaseWindow to Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}

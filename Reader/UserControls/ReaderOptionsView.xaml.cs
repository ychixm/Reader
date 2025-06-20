using System.Windows.Controls;
using Utils.Models; // For TabOverflowMode
using System; // For Enum
using System.Linq; // For Enum.GetValues
using System.Windows; // For RoutedEventArgs

namespace Reader.UserControls
{
    public partial class ReaderOptionsView : UserControl
    {
        public ReaderOptionsView()
        {
            InitializeComponent(); // This must be first
            TabOverflowComboBox.ItemsSource = Enum.GetValues(typeof(Utils.Models.TabOverflowMode));
            // The DataContext is typically set by the ViewModel in GetView(), or in XAML for design-time.
            // If ReaderOptionsViewModel.GetView() sets DataContext = this (where 'this' is ReaderOptionsViewModel),
            // then that's appropriate. ReaderOptionsView itself shouldn't set its DataContext to itself
            // if it expects a ViewModel. The current XAML d:DataContext points to ReaderOptionsViewModel.

            BrowseButton.Click += BrowseButton_Click;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // This dialog is in System.Windows.Forms.dll.
            // Make sure the project references this assembly (done via UseWindowsForms in .csproj).
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            // Consider setting dialog.Description or other properties as needed.
            // dialog.ShowNewFolderButton = true; // If you want to allow creating new folders

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // DataContext should be Reader.ViewModels.ReaderOptionsViewModel
                if (DataContext is Reader.ViewModels.ReaderOptionsViewModel viewModel)
                {
                    viewModel.DefaultPath = dialog.SelectedPath;
                }
            }
        }
    }
}

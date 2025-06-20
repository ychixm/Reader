using System.Windows.Controls;
using Utils.Models; // For TabOverflowMode
using System; // For Enum, IntPtr
using System.Linq; // For Enum.GetValues
using System.Windows; // For RoutedEventArgs
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Windows.Interop; // For WindowInteropHelper
using WinRT.Interop; // For InitializeWithWindow

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

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            // It's good practice to set the view mode and suggested start location.
            folderPicker.ViewMode = PickerViewMode.Thumbnail; // or .List
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*"); // Required to be populated, even with "*" for folders

            // Get the current window's HWND
            IntPtr hwnd = new WindowInteropHelper(Window.GetWindow(this)).EnsureHandle();

            // Initialize the folder picker with the window handle (HWND).
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            StorageFolder? pickedFolder = null;
            try
            {
                pickedFolder = await folderPicker.PickSingleFolderAsync();
            }
            catch (Exception ex_picker)
            {
                Utils.LogService.LogError(ex_picker, "Error picking folder.");
            }
            if (pickedFolder != null) // Continue if successful
            {
                if (DataContext is Reader.ViewModels.ReaderOptionsViewModel viewModel)
                {
                    viewModel.DefaultPath = pickedFolder.Path;
                }
            }
        }
    }
}

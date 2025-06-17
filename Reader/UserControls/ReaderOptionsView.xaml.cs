using System.Windows.Controls;
using Utils.Models; // For TabOverflowMode
using System; // For Enum
using System.Linq; // For Enum.GetValues

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
        }
    }
}

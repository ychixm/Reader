using System.Windows;
using System.Windows.Controls;

namespace Reader.UserControls
{
    public partial class ReaderOptionsControl : UserControl
    {
        // Property to hold a reference to the parent ReaderUserControl or its DataContext/ViewModel
        public ReaderUserControl? ParentReaderUserControl { get; set; }

        public ReaderOptionsControl()
        {
            InitializeComponent();
        }

        // Event handlers for scroll options
        private void ScrollbarModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Scrollbar_Click(sender, e);
            // Uncheck other scroll options if they are grouped (optional, depends on desired behavior)
            // if (ArrowButtonsModeCheckBox != null) ArrowButtonsModeCheckBox.IsChecked = false;
            // if (TabDropdownModeCheckBox != null) TabDropdownModeCheckBox.IsChecked = false;
        }

        private void ArrowButtonsModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Arrows_Click(sender, e);
            // if (ScrollbarModeCheckBox != null) ScrollbarModeCheckBox.IsChecked = false;
            // if (TabDropdownModeCheckBox != null) TabDropdownModeCheckBox.IsChecked = false;
        }

        private void TabDropdownModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Dropdown_Click(sender, e);
            // if (ScrollbarModeCheckBox != null) ScrollbarModeCheckBox.IsChecked = false;
            // if (ArrowButtonsModeCheckBox != null) ArrowButtonsModeCheckBox.IsChecked = false;
        }

        // Method to initialize states from ReaderUserControl (e.g., current IsChecked values)
        public void InitializeStates()
        {
            if (ParentReaderUserControl == null) return;

            // Assuming ReaderUserControl exposes these properties or methods to get current state
            // This is a conceptual linking. Actual properties/methods might need to be created
            // on ReaderUserControl if they don't exist.
            // For example, if KeyboardArrowsOption was a public property on ReaderUserControl:
            // KeyboardArrowsOptionCheckBox.IsChecked = ParentReaderUserControl.KeyboardArrowsOptionPublicState;
            // GridClickOptionCheckBox.IsChecked = ParentReaderUserControl.GridClickOptionPublicState;
            // VisibleButtonsOptionCheckBox.IsChecked = ParentReaderUserControl.VisibleButtonsOptionPublicState;

            // For scroll modes, we might need a method in ReaderUserControl to tell us the current mode
            // Or, if the MenuItems' IsChecked state was bound to properties, bind these CheckBoxes similarly.
            // For now, this setup assumes direct event forwarding. State synchronization needs to be handled.
            // One simple way is for ReaderUserControl to update these when it creates ReaderOptionsControl.
        }
    }
}

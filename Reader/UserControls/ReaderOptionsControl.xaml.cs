using System.Windows;
using System.Windows.Controls;
using Reader.Models; // Required for NavigationMethod enum

namespace Reader.UserControls
{
    public partial class ReaderOptionsControl : UserControl
    {
        public ReaderUserControl? ParentReaderUserControl { get; set; }

        public ReaderOptionsControl()
        {
            InitializeComponent();
            // Defer LoadSettings until ParentReaderUserControl is set,
            // or handle it in the property setter for ParentReaderUserControl.
        }

        // Call this method after ParentReaderUserControl is set.
        public void LoadSettings()
        {
            if (ParentReaderUserControl == null) return;

            KeyboardArrowsOptionCheckBox.IsChecked = ParentReaderUserControl.IsKeyboardArrowsNavigationEnabled;
            GridClickOptionCheckBox.IsChecked = ParentReaderUserControl.IsGridClickNavigationEnabled;
            VisibleButtonsOptionCheckBox.IsChecked = ParentReaderUserControl.IsVisibleButtonsNavigationEnabled;

            // Also load states for scroll mode checkboxes
            // This requires ParentReaderUserControl to expose its CurrentTabOverflowMode
            // For this example, let's assume CurrentTabOverflowMode gives the active mode.
            var currentScrollMode = ParentReaderUserControl.CurrentTabOverflowMode;
            ScrollbarModeCheckBox.IsChecked = (currentScrollMode == Utils.Models.TabOverflowMode.Scrollbar);
            ArrowButtonsModeCheckBox.IsChecked = (currentScrollMode == Utils.Models.TabOverflowMode.ArrowButtons);
            TabDropdownModeCheckBox.IsChecked = (currentScrollMode == Utils.Models.TabOverflowMode.TabDropdown);
        }

        private void PageSwapOption_Changed(object sender, RoutedEventArgs e)
        {
            if (ParentReaderUserControl == null) return;

            NavigationMethod currentMethods = NavigationMethod.None;
            if (KeyboardArrowsOptionCheckBox.IsChecked == true)
                currentMethods |= NavigationMethod.KeyboardArrows;
            if (GridClickOptionCheckBox.IsChecked == true)
                currentMethods |= NavigationMethod.GridClick;
            if (VisibleButtonsOptionCheckBox.IsChecked == true)
                currentMethods |= NavigationMethod.VisibleButtons;

            // Optional: Logic to ensure at least one option is selected, if required by design.
            // if (currentMethods == NavigationMethod.None && sender is CheckBox cb && cb.IsChecked == false)
            // {
            //     // Re-check the one that was just unchecked.
            //     cb.IsChecked = true;
            //     // Need to re-evaluate currentMethods if we re-check one.
            //     // This simple re-check might not be enough if the event triggers again.
            //     // A more robust way is needed if "at least one" is a hard rule.
            //     // For example, update currentMethods again before calling UpdateNavigationOptions.
            //     // currentMethods |= (NavigationMethod)Enum.Parse(typeof(NavigationMethod), cb.Tag.ToString()); // Assuming Tag has enum string name
            //     MessageBox.Show("At least one page swap option must be selected.", "Info");
            //     // return; // Or prevent saving this state.
            // }

            ParentReaderUserControl.UpdateNavigationOptions(currentMethods);
        }

        // Scroll options event handlers (already implemented to call ParentReaderUserControl methods)
        private void ScrollbarModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Scrollbar_Click(sender, e);
            if (ParentReaderUserControl != null && ScrollbarModeCheckBox.IsChecked == true) {
                 ArrowButtonsModeCheckBox.IsChecked = false;
                 TabDropdownModeCheckBox.IsChecked = false;
                 // ParentReaderUserControl.CurrentTabOverflowMode = Utils.Models.TabOverflowMode.Scrollbar; // This would call the setter logic in parent
            }
        }

        private void ArrowButtonsModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Arrows_Click(sender, e);
            if (ParentReaderUserControl != null && ArrowButtonsModeCheckBox.IsChecked == true) {
                 ScrollbarModeCheckBox.IsChecked = false;
                 TabDropdownModeCheckBox.IsChecked = false;
                 // ParentReaderUserControl.CurrentTabOverflowMode = Utils.Models.TabOverflowMode.ArrowButtons;
            }
        }

        private void TabDropdownModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ParentReaderUserControl?.SetOverflowMode_Dropdown_Click(sender, e);
            if (ParentReaderUserControl != null && TabDropdownModeCheckBox.IsChecked == true) {
                 ScrollbarModeCheckBox.IsChecked = false;
                 ArrowButtonsModeCheckBox.IsChecked = false;
                 // ParentReaderUserControl.CurrentTabOverflowMode = Utils.Models.TabOverflowMode.TabDropdown;
            }
        }
    }
}

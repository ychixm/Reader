using System.Windows;
using System.Collections.Generic; // For List
using System.Windows.Controls; // For TabControl, TabItem
using Reader.UserControls;
using Reader.Business;
using Reader;
using Utils;
using Reader.Models;
using Utils.Models; // Added using statement

namespace Assistant
{
    /// <summary>
    /// Interaction logic for MainFrame.xaml
    /// </summary>
    public partial class MainFrame : Window
    {
        public TabOverflowMode CurrentTabOverflowMode { get; set; } // Added property

        public MainFrame()
        {
            InitializeComponent();
            this.CurrentTabOverflowMode = TabOverflowMode.Scrollbar; // Added line
            this.DataContext = this; // Added line
        }

        private void MainFrame_Loaded(object sender, RoutedEventArgs e)
        {
            // Existing code (theming) can remain if present.

            if (MainTabControl != null && MyOptionsUserControl != null)
            {
                List<object> tabContents = new List<object>();
                foreach (var item in MainTabControl.Items)
                {
                    if (item is TabItem tabItem)
                    {
                        // We are interested in the content of the tab, not the OptionsUserControl itself
                        if (tabItem.Content != MyOptionsUserControl)
                        {
                            tabContents.Add(tabItem.Content);
                        }
                    }
                    // If items are directly bound (not TabItems), handle accordingly.
                    // This example assumes items are TabItems or their Content is directly the UserControl.
                    // If MainTabControl.ItemsSource is used and items are not TabItems:
                    else if (item != MyOptionsUserControl) // Check if the item itself is not the options control
                    {
                        // This case handles scenarios where TabControl.ItemsSource is bound to a collection
                        // of viewmodels or content objects, and TabControl generates TabItems implicitly.
                        // However, the current XAML explicitly defines TabItems.
                        // For this XAML, the `item is TabItem` check is primary.
                        // This secondary check is more of a fallback if structure changes.
                        tabContents.Add(item);
                    }
                }
                MyOptionsUserControl.PopulateOptions(tabContents);
            }
        }
    }
}

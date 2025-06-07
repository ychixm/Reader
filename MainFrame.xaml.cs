using System.Windows;
using System.Windows.Controls; // Required for TabItem
using Reader.UserControls;    // Required for ImageViewerAppControl

namespace Reader
{
    public partial class MainFrame : Window
    {
        public MainFrame()
        {
            InitializeComponent();
            // Ensure the button click handler is assigned if the button exists by this name
            if (this.FindName("LaunchReaderAppButton") is Button launchButton)
            {
                launchButton.Click += LaunchReaderAppButton_Click;
            }
        }

        private void LaunchReaderAppButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if a Reader App tab already exists
            foreach (TabItem existingTab in MainAppTabControl.Items)
            {
                if (existingTab.Header.ToString() == "Reader")
                {
                    MainAppTabControl.SelectedItem = existingTab; // Select existing tab
                    return;
                }
            }

            // Create a new instance of the Reader App UserControl
            ImageViewerAppControl readerAppControl = new ImageViewerAppControl();

            // Create a new TabItem
            TabItem readerTab = new TabItem
            {
                Header = "Reader", // Set the tab header
                Content = readerAppControl // Set the content to the UserControl
            };

            // Add the new TabItem to the MainAppTabControl
            MainAppTabControl.Items.Add(readerTab);

            // Select the newly added tab
            MainAppTabControl.SelectedItem = readerTab;
        }
    }
}

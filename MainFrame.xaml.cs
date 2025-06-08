using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Reader.UserControls; // Required for OptionsTabContentControl and ImageViewerAppControl
using ReaderUtils;
using System.Collections.Generic;
using System.Linq;

namespace Reader
{
    public partial class MainFrame : Window
    {
        public MainFrame()
        {
            InitializeComponent();
            if (this.FindName("LaunchReaderAppButton") is Button launchButton)
            {
                launchButton.Click += LaunchReaderAppButton_Click;
            }
            Loaded += (s, e) => {
                EnsureOptionsTabIsLast();
                // Initial update for Options tab if it's selected by default (it's not currently)
                // or if other tabs are loaded before it becomes visible.
                UpdateOptionsTabIfNeeded();
            };
            // MainAppTabControl_SelectionChanged is wired in XAML
        }

        private void LaunchReaderAppButton_Click(object sender, RoutedEventArgs e)
        {
            TabItem? existingAppTab = MainAppTabControl.Items.OfType<TabItem>()
                                        .FirstOrDefault(tab => tab.Content is ImageViewerAppControl);

            if (existingAppTab != null)
            {
                MainAppTabControl.SelectedItem = existingAppTab;
            }
            else
            {
                ImageViewerAppControl readerAppControl = new ImageViewerAppControl();
                TabItem readerTab = new TabItem
                {
                    Header = "Reader",
                    Content = readerAppControl
                };
                MainAppTabControl.Items.Add(readerTab);
                MainAppTabControl.SelectedItem = readerTab;
            }
            EnsureOptionsTabIsLast();
            UpdateOptionsTabIfNeeded(); // Update if Options tab is selected
        }

        private void MainAppTabControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                var nonClosableHeaders = new List<string> { "Modules" };
                TabItem? optionsTab = FindOptionsTabByName();
                if (optionsTab != null && optionsTab.Header != null)
                {
                    nonClosableHeaders.Add(optionsTab.Header.ToString()!);
                }

                bool tabClosed = WpfHelpers.HandleTabMiddleClickClose(MainAppTabControl, e.OriginalSource, nonClosableHeaders);

                if (tabClosed)
                {
                    EnsureOptionsTabIsLast();
                    UpdateOptionsTabIfNeeded(); // Update if Options tab is selected
                }
            }
        }

        private void MainAppTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && tc.Name == "MainAppTabControl") // Ensure it's our main TabControl
            {
                UpdateOptionsTabIfNeeded();
            }
        }

        private void UpdateOptionsTabIfNeeded()
        {
            TabItem? optionsTab = FindOptionsTabByName();
            if (optionsTab != null && MainAppTabControl.SelectedItem == optionsTab)
            {
                if (this.FindName("MyOptionsContentControl") is OptionsTabContentControl optionsContent)
                {
                    optionsContent.UpdateDisplayedOptions(MainAppTabControl, new[] { "Modules", "Options" });
                }
            }
        }

        private void EnsureOptionsTabIsLast()
        {
            TabItem? optionsTab = FindOptionsTabByName();
            if (optionsTab != null)
            {
                if (MainAppTabControl.Items.IndexOf(optionsTab) < MainAppTabControl.Items.Count - 1)
                {
                    object? selectedItem = MainAppTabControl.SelectedItem;
                    MainAppTabControl.Items.Remove(optionsTab);
                    MainAppTabControl.Items.Add(optionsTab);

                    if (selectedItem != null && selectedItem != optionsTab) {
                         MainAppTabControl.SelectedItem = selectedItem;
                    } else if (MainAppTabControl.Items.Count > 0 && MainAppTabControl.SelectedItem == null) {
                         MainAppTabControl.SelectedItem = optionsTab;
                    } else if (MainAppTabControl.SelectedItem == null && MainAppTabControl.Items.Count == 1 && MainAppTabControl.Items[0] == optionsTab) {
                        MainAppTabControl.SelectedItem = optionsTab;
                    }
                }
            }
        }

        private TabItem? FindOptionsTabByName()
        {
            return this.FindName("OptionsTab") as TabItem;
        }
    }
}

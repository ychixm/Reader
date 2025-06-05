using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Reader.Business;
using Reader.UserControls;
using System; // Required for EventArgs and TimeSpan
using System.Windows.Threading; // Required for DispatcherTimer

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<ChapterListElement> Views;
        private const int MaxTitleLength = 40; // Define the maximum character limit for the title
        private System.Windows.Threading.DispatcherTimer _resizeTimer;
        private int _lastColumnCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            Views = [];
            LoadChapterListAsync();
            UpdateGridLayout();

            _resizeTimer = new System.Windows.Threading.DispatcherTimer();
            _resizeTimer.Interval = TimeSpan.FromMilliseconds(200);
            _resizeTimer.Tick += ResizeTimer_Tick;
        }

        private void ResizeTimer_Tick(object? sender, EventArgs e)
        {
            _resizeTimer.Stop();
            UpdateGridLayout();
        }

        private void UpdateGridLayout()
        {
            // Ensure running on UI thread - this method is called by UI events or
            // after async operations marshalling to UI, so it should be on UI thread.
            // If not, Dispatcher.Invoke would be needed. Assuming it's called correctly.

            if (ChapterListGrid.ActualWidth == 0) // Grid not yet rendered
            {
                return;
            }

            double availableSpace = ChapterListGrid.ActualWidth;
            int newColumnCount = (int)Math.Max(1, Math.Floor(availableSpace / ChapterListElement.DesignWidth));

            // Early exit if column count and item count haven't changed
            if (newColumnCount == _lastColumnCount && ChapterListGrid.Children.Count == Views.Count)
            {
                bool allInPlace = true;
                for(int i = 0; i < Views.Count; i++)
                {
                    var view = Views[i];
                    int expectedRow = i / newColumnCount;
                    int expectedCol = i % newColumnCount;
                    if (Grid.GetRow(view) != expectedRow || Grid.GetColumn(view) != expectedCol || view.Parent != ChapterListGrid)
                    {
                        allInPlace = false;
                        break;
                    }
                }
                if (allInPlace) return;
            }

            // Update Column Definitions only if column count changed
            if (newColumnCount != _lastColumnCount || ChapterListGrid.ColumnDefinitions.Count != newColumnCount)
            {
                ChapterListGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < newColumnCount; i++)
                {
                    ChapterListGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }
                _lastColumnCount = newColumnCount;
            }

            // Row definitions are simpler to clear and rebuild as needed during element placement
            ChapterListGrid.RowDefinitions.Clear();

            // Ensure all views are in the grid and correctly positioned
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view.Parent != ChapterListGrid)
                {
                     // If view was parented to something else, it would need to be removed from old parent first.
                     // Assuming ChapterListElement instances are only parented to this grid or are new.
                    ChapterListGrid.Children.Add(view);
                }

                int row = i / newColumnCount;
                int column = i % newColumnCount;

                Grid.SetRow(view, row);
                Grid.SetColumn(view, column);

                // Add new row definitions as needed
                if (ChapterListGrid.RowDefinitions.Count <= row)
                {
                    ChapterListGrid.RowDefinitions.Add(new RowDefinition());
                }
            }

            // Remove any children from grid that are no longer in Views
            List<UIElement> childrenToRemove = new List<UIElement>();
            foreach (UIElement child in ChapterListGrid.Children)
            {
                if (child is ChapterListElement cle && !Views.Contains(cle))
                {
                    childrenToRemove.Add(child);
                }
            }
            foreach (UIElement childToRemove in childrenToRemove)
            {
                ChapterListGrid.Children.Remove(childToRemove);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private async void LoadChapterListAsync()
        {
            List<DirectoryInfo> chapters = await Task.Run(() => Tools.GetDirectories(""));

            foreach (var directory in chapters)
            {
                ChapterListElement chapterListElement = new(directory)
                {
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1),
                };

                chapterListElement.SetLabelText(directory.Name);
                // Set the NoImage.png as the default placeholder
                string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ressources", "NoImage.png");
                chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

                Views.Add(chapterListElement); // Add every chapter element to the list.
                UpdateGridLayout(); // <-- New call here

                // Now, try to load and set the actual image for it.
                var imageSourceUri = await Task.Run(() => Tools.GetFirstImageInDirectory(directory));

                if (imageSourceUri != null)
                {
                    var (width, height) = Tools.GetImageDimensions(imageSourceUri.LocalPath);

                    BitmapImage? finalThumbnail = await Task.Run(() => {
                        BitmapImage thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.UriSource = imageSourceUri;
                        if (width > height) { thumbnail.DecodePixelWidth = ChapterListElement.DesignWidth; }
                        else { thumbnail.DecodePixelHeight = ChapterListElement.ImageHeight; }
                        thumbnail.CreateOptions = BitmapCreateOptions.None;
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                        thumbnail.EndInit();
                        thumbnail.Freeze();
                        return thumbnail;
                    });

                    if (finalThumbnail != null)
                    {
                        // Update the existing element's image
                        chapterListElement.SetImageSource(finalThumbnail);
                    }
                    // If finalThumbnail is null, it keeps the default empty image set earlier.
                }
            }

            // Update the grid layout after all chapters have been loaded
            // UpdateGridLayout(); // <-- This call should be removed/commented

            // Update the text to "Chapters (Loaded)"
            MainTabHeaderTextBlock.Text += " (Loaded)";
        }

        private void ChapterListElement_Loaded(object? sender, EventArgs e)
        {
            // Update the list when a ChapterListElement has finished loading
        }

        public void AddImageTab(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            // Check if a tab for the directory already exists
            var existingTab = MainTabControl.Items.OfType<TabItem>()
                .FirstOrDefault(tab => tab.Header.ToString() == $"Images - {Path.GetFileName(directoryPath)}");

            if (existingTab != null)
            {
                if (switchToTab)
                {
                    MainTabControl.SelectedItem = existingTab;
                }
                return;
            }

            var imageTabControl = new ImageTabControl(imagePaths);
            string tabTitle = Path.GetFileName(directoryPath);

            // Truncate the title if it exceeds the maximum character limit
            if (tabTitle.Length > MaxTitleLength)
            {
                tabTitle = string.Concat(tabTitle.AsSpan(0, MaxTitleLength), "...");
            }

            var tabItem = new TabItem
            {
                Header = tabTitle,
                Content = imageTabControl
            };

            MainTabControl.Items.Add(tabItem);
            if (switchToTab)
            {
                MainTabControl.SelectedItem = tabItem;
            }
        }

        private void MainTabControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                var tabItem = Tools.FindParent<TabItem>((DependencyObject)e.OriginalSource);
                if (tabItem != null && tabItem != MainTab)
                {
                    MainTabControl.Items.Remove(tabItem);
                }
            }
        }

        
    }
}

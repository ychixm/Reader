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
            if (ChapterListGrid.Dispatcher.CheckAccess())
            {
                ChapterListGrid.Children.Clear();
                ChapterListGrid.RowDefinitions.Clear();
                ChapterListGrid.ColumnDefinitions.Clear();

                double availableSpace = ChapterListGrid.ActualWidth;
                int columns = (int)Math.Max(1, Math.Floor(availableSpace / ChapterListElement.DesignWidth));

                for (int i = 0; i < columns; i++)
                {
                    ChapterListGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }

                int row = 0;
                int column = 0;
                foreach (var view in Views)
                {
                    if (column == columns)
                    {
                        column = 0;
                        row++;
                        ChapterListGrid.RowDefinitions.Add(new RowDefinition());
                    }

                    Grid.SetRow(view, row);
                    Grid.SetColumn(view, column);
                    ChapterListGrid.Children.Add(view);
                    column++;
                }
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

                var imageSourceUri = await Task.Run(() => Tools.GetFirstImageInDirectory(directory));
                if (imageSourceUri != null)
                {
                    // Assuming Tools.GetImageDimensions is fast. If not, it should also be run asynchronously.
                    var (width, height) = Tools.GetImageDimensions(imageSourceUri.LocalPath);

                    BitmapImage? finalThumbnail = await Task.Run(() => {
                        BitmapImage thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.UriSource = imageSourceUri; // UriSource is set on the background thread.

                        // Set decode properties based on dimensions.
                        if (width > height)
                        {
                            thumbnail.DecodePixelWidth = ChapterListElement.DesignWidth;
                        }
                        else
                        {
                            thumbnail.DecodePixelHeight = ChapterListElement.ImageHeight;
                        }

                        // Ensure the image is decoded on this background thread and not on the UI thread.
                        thumbnail.CreateOptions = BitmapCreateOptions.None;
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad;

                        thumbnail.EndInit(); // This performs the decoding.
                        thumbnail.Freeze();  // Allow the BitmapImage to be used on the UI thread.
                        return thumbnail;
                    });

                    // Operations below this line are back on the UI thread due to 'await'.
                    chapterListElement.Loaded += ChapterListElement_Loaded;

                    if (finalThumbnail == null)
                    {
                        // If thumbnail creation failed or resulted in null, use an empty BitmapImage.
                        // This matches the original code's behavior for a null uiImageSource.
                        chapterListElement.SetImageSource(new BitmapImage(new Uri("")));
                    }
                    else
                    {
                        chapterListElement.SetImageSource(finalThumbnail);
                    }

                    chapterListElement.SetLabelText(directory.Name);

                    Views.Add(chapterListElement);

                    chapterListElement.IsFinished();
                }
            }

            // Update the grid layout after all chapters have been loaded
            UpdateGridLayout();

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

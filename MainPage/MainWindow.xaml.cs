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

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<ChapterListElement> Views;
        private const int MaxTitleLength = 40; // Define the maximum character limit for the title

        public MainWindow()
        {
            InitializeComponent();
            Views = [];
            LoadChapterListAsync();
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
            UpdateGridLayout();
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
                    // Get image dimensions. Consider making Tools.GetImageDimensions async if it's slow.
                    var (width, height) = Tools.GetImageDimensions(imageSourceUri.LocalPath);

                    BitmapImage? uiImageSource = null;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        uiImageSource = new BitmapImage();
                        uiImageSource.BeginInit();
                        uiImageSource.UriSource = imageSourceUri; // imageSourceUri from the outer scope
                        if (width > height) // width & height from outer scope
                        {
                            uiImageSource.DecodePixelWidth = ChapterListElement.DesignWidth;
                        }
                        else
                        {
                            uiImageSource.DecodePixelHeight = ChapterListElement.ImageHeight;
                        }
                        uiImageSource.EndInit();
                        uiImageSource.Freeze(); // Important for performance and cross-thread access
                    });

                    chapterListElement.Loaded += ChapterListElement_Loaded;
                    if (uiImageSource == null)
                    {
                        chapterListElement.SetImageSource(new BitmapImage(new Uri("")));
                    }
                    else
                    {
                        chapterListElement.SetImageSource(uiImageSource);
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
            UpdateGridLayout();
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

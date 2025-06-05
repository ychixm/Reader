using System.Collections.Generic;
using System.Collections.ObjectModel; // Was already present
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
using System;
using System.Diagnostics; // Added for Debug.WriteLine
// System.Windows.Threading is no longer needed as DispatcherTimer is removed

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Changed Views to a public auto-property with initializer
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40; // Define the maximum character limit for the title
        // Removed _resizeTimer field
        // Removed _lastColumnCount field

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this; // Set DataContext
            // Views is initialized by its property initializer, removed Views = [];
            LoadChapterListAsync();
            // Removed _resizeTimer initialization
            // Removed initial UpdateGridLayout() call
        }

        // Removed ResizeTimer_Tick method
        // Removed UpdateGridLayout method
        // Removed MainWindow_SizeChanged method

        private async void LoadChapterListAsync()
        {
            Debug.WriteLine("LoadChapterListAsync - Started.");
            List<DirectoryInfo> chapters = await Task.Run(() => Tools.GetDirectories(""));

            foreach (var directory in chapters)
            {
                ChapterListElement chapterListElement = new(directory)
                {
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1),
                };

                chapterListElement.SetLabelText(directory.Name);
                string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ressources", "NoImage.png");
                chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

                Views.Add(chapterListElement); // Add every chapter element to the list.
                Debug.WriteLine($"LoadChapterListAsync - Added chapter: {directory.Name}. Views count: {Views.Count}");
                // Removed UpdateGridLayout() call from here

                var imageSourceUri = await Task.Run(() => Tools.GetFirstImageInDirectory(directory));

                if (imageSourceUri != null)
                {
                    BitmapImage? finalThumbnail = await Task.Run(() => {
                        var (width, height) = Tools.GetImageDimensions(imageSourceUri.LocalPath);
                        BitmapImage thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.UriSource = imageSourceUri;
                        if (width > height) { thumbnail.DecodePixelWidth = (int)ChapterListElement.DesignWidth; }
                        else { thumbnail.DecodePixelHeight = ChapterListElement.ImageHeight; }
                        thumbnail.CreateOptions = BitmapCreateOptions.None;
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                        thumbnail.EndInit();
                        thumbnail.Freeze();
                        return thumbnail;
                    });

                    if (finalThumbnail != null)
                    {
                        chapterListElement.SetImageSource(finalThumbnail);
                    }
                }
            }
            Debug.WriteLine($"LoadChapterListAsync - Finished loop. Final Views count: {Views.Count}");
            // Removed final UpdateGridLayout() call from here
            MainTabHeaderTextBlock.Text += " (Loaded)";
        }

        private void ChapterListElement_Loaded(object? sender, EventArgs e)
        {
            // This method is currently empty and was previously used for UpdateGridLayout.
            // It can be removed if no longer needed for other purposes. For now, it's kept as empty.
        }

        public void AddImageTab(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            var existingTab = MainTabControl.Items.OfType<TabItem>()
                .FirstOrDefault(tab => tab.Tag is string path && path == directoryPath);

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

            if (tabTitle.Length > MaxTitleLength)
            {
                tabTitle = string.Concat(tabTitle.AsSpan(0, MaxTitleLength), "...");
            }

            var tabItem = new TabItem
            {
                Header = tabTitle,
                Content = imageTabControl,
                Tag = directoryPath
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

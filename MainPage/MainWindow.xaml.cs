using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq; // This seems unused, consider removing if no XAML resources or LINQ to XML is used.
using Reader.Business;
using Reader.UserControls;
using System;
using System.Diagnostics;
using System.Threading.Tasks; // Added for Task

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";

        /// <summary>
        /// Gets the collection of ChapterListElement items to be displayed.
        /// This collection is bound to the ItemsControl in the XAML.
        /// </summary>
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            LoadChapterListAsync();
        }

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            ChapterListElement chapterListElement = new(directory)
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };

            chapterListElement.SetLabelText(directory.Name);
            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

            Views.Add(chapterListElement);
            Debug.WriteLine($"LoadChapterListAsync - Added chapter: {directory.Name}. Views count: {Views.Count}");

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

        private async void LoadChapterListAsync()
        {
            Debug.WriteLine("LoadChapterListAsync - Started.");
            try
            {
                List<DirectoryInfo> chapters = await Task.Run(() => Tools.GetDirectories(""));

                foreach (var directory in chapters)
                {
                    await ProcessChapterDirectoryAsync(directory);
                }
                Debug.WriteLine($"LoadChapterListAsync - Finished loop. Final Views count: {Views.Count}");
                MainTabHeaderTextBlock.Text += " (Loaded)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadChapterListAsync - An error occurred: {ex.Message}");
                // Optionally, update the UI to show an error message
            }
        }

        // Deleted ChapterListElement_Loaded method

        /// <summary>
        /// Adds a new tab for displaying images from a specified directory path,
        /// or selects an existing tab if one for the directory already exists.
        /// </summary>
        /// <param name="directoryPath">The full path to the directory containing images.</param>
        /// <param name="imagePaths">A list of full paths to the images within the directory.</param>
        /// <param name="switchToTab">True to select the tab after adding/finding it; false otherwise.</param>
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
            string tabTitle = Path.GetFileName(directoryPath); // System.IO.Path

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

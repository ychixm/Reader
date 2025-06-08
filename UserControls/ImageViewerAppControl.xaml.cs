using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reader.Business; // For AppSettingsService
using Reader.Models;   // For ChapterOpenRequestedEventArgs, AppSettings (if not fully qualified)
// TabOverflowManager and TabOverflowMode are now in ReaderUtils
using ReaderUtils;     // For WpfHelpers, FileSystemHelpers (if used directly)
// using ReaderUtils.Business; // No longer directly using TabOverflowManager here
// using ReaderUtils.Models;   // No longer directly using TabOverflowMode enum here

namespace Reader.UserControls
{
    public partial class ImageViewerAppControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        private Reader.Models.AppSettings _settings; // Fully qualify if needed, or ensure correct using

        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };
        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";
        private const int MaxTitleLength = 40;
        private bool _isDisposed = false;

        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();

        public ImageViewerAppControl()
        {
            InitializeComponent();
            this.DataContext = this;

            try
            {
                // AppSettingsService is in Reader.Business
                _settings = AppSettingsService.LoadAppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AppSettings: {ex.Message}");
                _settings = new Reader.Models.AppSettings(); // Fully qualify
            }

            ChaptersGrid.ItemsSource = Views;
            _ = LoadChapterListAsync();

            // Loaded and Unloaded events are still relevant for this UserControl's lifecycle
            this.Loaded += ImageViewerAppControl_Loaded;
            this.Unloaded += ImageViewerAppControl_Unloaded;
        }

        private void ImageViewerAppControl_Loaded(object sender, RoutedEventArgs e)
        {
            // TabOverflowManager and ComboBox logic is now encapsulated in TabOverflowOptionsControl.
            // No specific initialization needed here for it anymore.
        }

        private void ImageViewerAppControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        // CurrentTabOverflowMode property is removed.
        // TabOverflowModeComboBox_SelectionChanged method is removed.

        private async Task LoadChapterListAsync()
        {
            if (Application.Current == null)
            {
                System.Diagnostics.Debug.WriteLine("LoadChapterListAsync: Application.Current is null. Cannot proceed.");
                return;
            }
            await Application.Current.Dispatcher.InvokeAsync(() => Views.Clear());
            try
            {
                string effectivePath = _settings.DefaultPath;
                if (string.IsNullOrEmpty(effectivePath) || !Directory.Exists(effectivePath))
                {
                    effectivePath = AppDomain.CurrentDomain.BaseDirectory;
                }
                // FileSystemHelpers is in ReaderUtils
                List<DirectoryInfo> chapters = await Task.Run(() => ReaderUtils.FileSystemHelpers.GetDirectories(effectivePath));
                foreach (var directory in chapters)
                {
                    if (_isDisposed || Application.Current == null) break;
                    // ChapterOpenRequestedEventArgs is in Reader.Models
                    await ProcessChapterDirectoryAsync(directory);
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"LoadChapterListAsync - An error occurred: {ex.Message}");
            }
        }

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            if (_isDisposed || Application.Current == null) return;
            ChapterListElement chapterListElement = new(directory)
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };
            chapterListElement.ChapterOpenRequested += HandleChapterOpenRequested;
            chapterListElement.SetLabelText(directory.Name);
            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            try
            {
                BitmapImage placeholderBmp = new BitmapImage();
                placeholderBmp.BeginInit();
                placeholderBmp.UriSource = new Uri(placeholderPath, UriKind.Absolute);
                placeholderBmp.CacheOption = BitmapCacheOption.OnLoad;
                placeholderBmp.EndInit();
                placeholderBmp.Freeze();
                chapterListElement.SetImageSource(placeholderBmp);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading placeholder for {directory.Name}: {ex.Message}");
            }
            if (_isDisposed || Application.Current == null) return;
            await Application.Current.Dispatcher.InvokeAsync(() => Views.Add(chapterListElement));
            try
            {
                // FileSystemHelpers is in ReaderUtils
                var imageSourceUri = await Task.Run(() => ReaderUtils.FileSystemHelpers.GetFirstFileByExtensions(directory, SupportedImageExtensions));
                if (imageSourceUri != null)
                {
                    // FileSystemHelpers is in ReaderUtils
                    BitmapImage? finalThumbnail = await Task.Run(() => {
                        var (width, height) = ReaderUtils.FileSystemHelpers.GetImageDimensions(imageSourceUri.LocalPath);
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
                    if (finalThumbnail != null && !_isDisposed)
                    {
                        chapterListElement.SetImageSource(finalThumbnail);
                    }
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing thumbnail for {directory.Name}: {ex.Message}");
            }
        }

        // ChapterOpenRequestedEventArgs is in Reader.Models
        private void HandleChapterOpenRequested(object? sender, Reader.Models.ChapterOpenRequestedEventArgs e)
        {
            if (_isDisposed) return;
            AddImageTab(e.DirectoryPath, e.ImagePaths, e.SwitchToTab);
        }

        public void AddImageTab(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            if (_isDisposed || InternalTabControl == null) return;
            var existingTab = InternalTabControl.Items.OfType<TabItem>()
                .FirstOrDefault(tab => tab.Tag is string path && path == directoryPath);
            if (existingTab != null)
            {
                if (switchToTab) InternalTabControl.SelectedItem = existingTab;
                return;
            }
            // ImageTabControl is in Reader.UserControls
            var imageTabControl = new Reader.UserControls.ImageTabControl(imagePaths);
            string? tabTitle = Path.GetFileName(directoryPath);
            if (tabTitle != null && tabTitle.Length > MaxTitleLength)
            {
                tabTitle = string.Concat(tabTitle.AsSpan(0, MaxTitleLength), "...");
            }
            else if (string.IsNullOrEmpty(tabTitle))
            {
                tabTitle = "Unknown";
            }
            var tabItem = new TabItem
            {
                Header = tabTitle,
                Content = imageTabControl,
                Tag = directoryPath
            };
            InternalTabControl.Items.Add(tabItem);
            if (switchToTab) InternalTabControl.SelectedItem = tabItem;
        }

        private void InternalTabControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && InternalTabControl != null)
            {
                // WpfHelpers is in ReaderUtils
                ReaderUtils.WpfHelpers.HandleTabMiddleClickClose(InternalTabControl, e.OriginalSource, new[] { "Chapters" });
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                _isDisposed = true;
                this.Loaded -= ImageViewerAppControl_Loaded;
                this.Unloaded -= ImageViewerAppControl_Unloaded;
                if (Views != null)
                {
                    foreach(var view in Views.ToList())
                    {
                        view.ChapterOpenRequested -= HandleChapterOpenRequested;
                    }
                    Views.Clear();
                }
                if (InternalTabControl != null)
                {
                    foreach (var item in InternalTabControl.Items.OfType<TabItem>().ToList())
                    {
                        if (item.Content is IDisposable disposableContent) disposableContent.Dispose();
                    }
                    InternalTabControl.Items.Clear();
                }
            }
            _isDisposed = true; // Ensure it's set even if disposing is false (from finalizer)
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_isDisposed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        ~ImageViewerAppControl()
        {
            Dispose(false);
        }
    }
}

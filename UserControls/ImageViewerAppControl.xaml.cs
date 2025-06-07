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
using Reader.Business;
using Reader.Models;
using ReaderUtils; // Assuming ReaderUtils is the correct namespace after renaming

namespace Reader.UserControls
{
    public partial class ImageViewerAppControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        private AppSettings _settings;
        private TabOverflowManager? _tabOverflowManager;

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
                _settings = AppSettingsService.LoadAppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AppSettings: {ex.Message}");
                // Initialize with default settings if loading fails
                _settings = new AppSettings();
            }

            ChaptersGrid.ItemsSource = Views;
            // Ensure LoadChapterListAsync is not blocking UI thread if called from constructor directly
            // and handles potential null Application.Current if called too early or in test environment.
            _ = LoadChapterListAsync();

            this.Loaded += ImageViewerAppControl_Loaded;
            this.Unloaded += ImageViewerAppControl_Unloaded;
        }

        private void ImageViewerAppControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure InternalTabControl is not null
            if (InternalTabControl != null)
            {
                var ownerWindow = WpfHelpers.FindParent<Window>(this);
                if (ownerWindow != null)
                {
                    // Calling the 2-argument constructor of TabOverflowManager
                    _tabOverflowManager = new TabOverflowManager(InternalTabControl, ownerWindow);

                    // LoadPersistedTabOverflowMode is called in TabOverflowManager's constructor.
                    // Get the mode from the manager after it's initialized.
                    var persistedMode = _tabOverflowManager.CurrentTabOverflowMode;

                    // Ensure ComboBox is not null before setting its SelectedIndex
                    if (TabOverflowModeComboBox != null)
                    {
                        TabOverflowModeComboBox.SelectedIndex = (int)persistedMode;
                    }
                    // SetOverflowMode no longer needs updateUiElements; this logic is handled by CurrentTabOverflowMode setter
                    _tabOverflowManager.SetOverflowMode(persistedMode);
                    OnPropertyChanged(nameof(CurrentTabOverflowMode));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ImageViewerAppControl_Loaded: Owner window not found.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ImageViewerAppControl_Loaded: InternalTabControl is null.");
            }
        }

        private void ImageViewerAppControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public TabOverflowMode CurrentTabOverflowMode
        {
            get => _tabOverflowManager?.CurrentTabOverflowMode ?? TabOverflowMode.Scrollbar;
            set
            {
                if (_tabOverflowManager != null && _tabOverflowManager.CurrentTabOverflowMode != value)
                {
                    _tabOverflowManager.SetOverflowMode(value); // updateUiElements parameter removed
                    OnPropertyChanged();
                }
            }
        }

        private void TabOverflowModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tabOverflowManager != null && TabOverflowModeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Content is string contentString && Enum.TryParse<TabOverflowMode>(contentString, out var mode))
                {
                    if (CurrentTabOverflowMode != mode)
                    {
                        CurrentTabOverflowMode = mode;
                    }
                }
            }
        }

        private async Task LoadChapterListAsync()
        {
            // Ensure operations modifying Views collection are on the UI thread.
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

                List<DirectoryInfo> chapters = await Task.Run(() => FileSystemHelpers.GetDirectories(effectivePath));

                foreach (var directory in chapters)
                {
                    if (_isDisposed || Application.Current == null) break;
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
                var imageSourceUri = await Task.Run(() => FileSystemHelpers.GetFirstFileByExtensions(directory, SupportedImageExtensions));

                if (imageSourceUri != null)
                {
                    BitmapImage? finalThumbnail = await Task.Run(() => {
                        var (width, height) = FileSystemHelpers.GetImageDimensions(imageSourceUri.LocalPath);
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

        private void HandleChapterOpenRequested(object? sender, ChapterOpenRequestedEventArgs e)
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

            var imageTabControl = new ImageTabControl(imagePaths);
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
            if (_isDisposed || InternalTabControl == null) return;

            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.OriginalSource is DependencyObject sourceObject)
                {
                    var tabItem = WpfHelpers.FindParent<TabItem>(sourceObject);
                    if (tabItem != null && tabItem != ChaptersTab)
                    {
                        InternalTabControl.Items.Remove(tabItem);
                        if (tabItem.Content is IDisposable disposableContent)
                        {
                            disposableContent.Dispose();
                        }
                        else if (tabItem.Content is ImageTabControl itc) // ImageTabControl has an Unloaded event that handles cleanup
                        {
                            // itc.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); // Not ideal, Unloaded is usually framework-driven
                        }
                    }
                }
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
                // Dispose managed state (managed objects).
                if (_tabOverflowManager != null)
                {
                    // If TabOverflowManager has a Dispose method or specific cleanup:
                    // _tabOverflowManager.Dispose();
                    _tabOverflowManager = null;
                }

                // Unsubscribe from events to prevent memory leaks
                this.Loaded -= ImageViewerAppControl_Loaded;
                this.Unloaded -= ImageViewerAppControl_Unloaded;

                if (Views != null)
                {
                    foreach(var view in Views)
                    {
                        // Assuming ChapterListElement doesn't need explicit Dispose.
                        // If it did, you'd call it here.
                        // Unsubscribe from its events if not done elsewhere or if element is reused.
                        view.ChapterOpenRequested -= HandleChapterOpenRequested;
                    }
                    Views.Clear();
                }

                // Clean up tabs in InternalTabControl
                if (InternalTabControl != null)
                {
                    foreach (var item in InternalTabControl.Items.OfType<TabItem>().ToList()) // ToList to modify collection
                    {
                        if (item.Content is IDisposable disposableContent)
                        {
                            disposableContent.Dispose();
                        }
                         // ImageTabControl handles its own cleanup on Unloaded
                        InternalTabControl.Items.Remove(item);
                    }
                }

            }
            _isDisposed = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_isDisposed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Finalizer in case Dispose is not called
        ~ImageViewerAppControl()
        {
            Dispose(false);
        }
    }
}

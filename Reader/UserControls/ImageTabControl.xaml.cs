using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Reader.Models;
using Utils;

namespace Reader.UserControls
{
    public partial class ImageTabControl : UserControl
    {
        private readonly ILoggerService _logger;
        private ReaderSettings _settings;
        private readonly List<string> _imagePaths;
        private int _currentIndex;

        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private readonly HashSet<string> _currentlyPreloading = new HashSet<string>();
        private CancellationTokenSource _preloadCts = new CancellationTokenSource();
        private const int PreloadNextCount = 2;
        private const int PreloadPrevCount = 1;

        private static BitmapImage? _errorPlaceholderImage;

        private void EnsureErrorPlaceholderLoaded()
        {
            if (_errorPlaceholderImage == null)
            {
                string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ressources", "NoImage.png");
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(placeholderPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _errorPlaceholderImage = bmp;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load error placeholder image for ImageTabControl at {PlaceholderPath}", placeholderPath);
                }
            }
        }

        public void Grid_Overall_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == LeftArrow || e.OriginalSource == RightArrow)
            {
                return;
            }

            if (_settings == null || !(_settings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick)))
            {
                return;
            }

            if (_imagePaths == null || _imagePaths.Count == 0 || DisplayedImage.Source == _errorPlaceholderImage)
            {
                return;
            }

            Point position = e.GetPosition(this);
            double controlWidth = this.ActualWidth;

            if (position.X < controlWidth * 0.33)
            {
                LeftArrow_Click(this, new RoutedEventArgs());
            }
            else if (position.X > controlWidth * 0.67)
            {
                RightArrow_Click(this, new RoutedEventArgs());
            }
        }

        public ImageTabControl(List<string> imagePaths, ILoggerService logger)
        {
            InitializeComponent();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            EnsureErrorPlaceholderLoaded();

            _settings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());
            AppSettingsService.SettingsChanged += HandleAppSettingsChanged;

            _imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
            _preloadCts = new CancellationTokenSource();

            if (_imagePaths.Count == 0)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                DisplayedImage.Source = _errorPlaceholderImage;
                _logger.LogInfo("ImageTabControl initialized with no image paths.");
                return;
            }

            _currentIndex = 0;
            _logger.LogInfo("ImageTabControl initializing with {ImagePathCount} images. Current index: {CurrentIndex}", _imagePaths.Count, _currentIndex);
            LoadAndDisplayImage(_currentIndex);

            this.Focusable = true;
            this.Focus();

            ApplyNavigationSettings();
        }

        private void ApplyNavigationSettings()
        {
            if (_settings == null)
            {
                _settings = new ReaderSettings();
            }

            bool showButtons = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);
            Visibility buttonVisibility = showButtons ? Visibility.Visible : Visibility.Collapsed;

            LeftArrow.Visibility = buttonVisibility;
            RightArrow.Visibility = buttonVisibility;
        }

        private BitmapImage? LoadBitmapImageFromFile(string imagePath, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;

            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(imagePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.EndInit();
                bmp.Freeze();
                return token.IsCancellationRequested ? null : bmp;
            }
            catch (Exception ex_load) when (!(ex_load is OperationCanceledException || ex_load is ArgumentException ))
            {
                _logger.LogWarning(ex_load.Message, "Failed to load bitmap image from file {ImagePath}.", imagePath);
                return null;
            }
        }

        private async void LoadAndDisplayImage(int index)
        {
            if (index < 0 || index >= _imagePaths.Count)
            {
                _logger.LogWarning("LoadAndDisplayImage called with invalid index: {Index}. Image count: {ImageCount}", index, _imagePaths.Count);
                DisplayedImage.Source = _errorPlaceholderImage;
                LoadingIndicator.Visibility = Visibility.Collapsed;
                return;
            }
            _currentIndex = index;
            string imagePath = _imagePaths[index];
            _logger.LogVerbose("LoadAndDisplayImage: Index {Index}, Path {ImagePath}", _currentIndex, imagePath);

            if (_preloadCts != null)
            {
                _preloadCts.Cancel();
                _preloadCts.Dispose();
            }
            _preloadCts = new CancellationTokenSource();
            CancellationToken currentToken = _preloadCts.Token;

            LoadingIndicator.Visibility = Visibility.Visible;
            DisplayedImage.Source = null;

            BitmapImage? bitmapToShow = null;

            if (_imageCache.TryGetValue(imagePath, out bitmapToShow))
            {
                _logger.LogDebug("Image {ImagePath} found in cache.", imagePath);
            }
            else
            {
                _logger.LogDebug("Image {ImagePath} not in cache. Loading from file.", imagePath);
                try
                {
                    if (currentToken.IsCancellationRequested)
                    {
                        _logger.LogInfo("Image loading cancelled for {ImagePath} before starting Task.Run.", imagePath);
                        DisplayedImage.Source = _errorPlaceholderImage;
                        LoadingIndicator.Visibility = Visibility.Collapsed;
                        return;
                    }

                    bitmapToShow = await Task.Run(() => LoadBitmapImageFromFile(imagePath, currentToken), currentToken);

                    if (bitmapToShow != null && !currentToken.IsCancellationRequested)
                    {
                        lock(_imageCache)
                        {
                            _imageCache[imagePath] = bitmapToShow;
                            _logger.LogDebug("Image {ImagePath} loaded and added to cache.", imagePath);
                        }
                    }
                    else if(currentToken.IsCancellationRequested)
                    {
                        _logger.LogInfo("Image loading cancelled for {ImagePath} after Task.Run.", imagePath);
                    }
                }
                catch (Exception ex_display) when (!(ex_display is OperationCanceledException))
                {
                    _logger.LogWarning(ex_display.Message, "Exception during LoadAndDisplayImage for path {ImagePath}.", imagePath);
                    bitmapToShow = null;
                }
            }

            if (currentToken.IsCancellationRequested)
            {
                _logger.LogInfo("Display update cancelled for {ImagePath}.", imagePath);
                DisplayedImage.Source = _errorPlaceholderImage; // Ensure placeholder on cancellation
                LoadingIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            if (bitmapToShow != null)
            {
                DisplayedImage.Source = bitmapToShow;
            }
            else
            {
                _logger.LogWarning("Bitmap for {ImagePath} is null after load attempt. Displaying error placeholder.", imagePath);
                DisplayedImage.Source = _errorPlaceholderImage;
            }

            LoadingIndicator.Visibility = Visibility.Collapsed;

            if (!currentToken.IsCancellationRequested)
            {
                _ = PreloadAdjacentImagesAsync(_currentIndex, currentToken);
            }
        }

        private async Task PreloadAdjacentImagesAsync(int currentIndex, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            _logger.LogVerbose("Starting preload for images adjacent to index {CurrentIndex}", currentIndex);
            List<Task> preloadTasks = new List<Task>();

            for (int i = 1; i <= PreloadNextCount; i++)
            {
                int nextIndex = currentIndex + i;
                if (nextIndex < _imagePaths.Count)
                {
                    preloadTasks.Add(EnsureImageLoadedAsync(_imagePaths[nextIndex], token));
                }
            }

            for (int i = 1; i <= PreloadPrevCount; i++)
            {
                int prevIndex = currentIndex - i;
                if (prevIndex >= 0)
                {
                    preloadTasks.Add(EnsureImageLoadedAsync(_imagePaths[prevIndex], token));
                }
            }

            try
            {
                await Task.WhenAll(preloadTasks);
                _logger.LogVerbose("Preload tasks completed for index {CurrentIndex}", currentIndex);
            }
            catch (Exception ex_preload_agg) when (!(ex_preload_agg is OperationCanceledException))
            {
                _logger.LogWarning(ex_preload_agg.Message, "Exception during PreloadAdjacentImagesAsync Task.WhenAll for current index {CurrentIndex}.", currentIndex);
            }
        }

        private async Task EnsureImageLoadedAsync(string imagePath, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogDebug("Preload cancelled for {ImagePath} (token).", imagePath);
                return;
            }
            if (_imageCache.ContainsKey(imagePath))
            {
                _logger.LogDebug("Image {ImagePath} already in cache, skipping preload.", imagePath);
                return;
            }

            bool shouldLoad = false;
            lock (_currentlyPreloading)
            {
                if (!_currentlyPreloading.Contains(imagePath))
                {
                    _currentlyPreloading.Add(imagePath);
                    shouldLoad = true;
                }
            }

            if (!shouldLoad)
            {
                _logger.LogDebug("Image {ImagePath} is already being preloaded by another task.", imagePath);
                return;
            }

            _logger.LogVerbose("Starting EnsureImageLoadedAsync for {ImagePath}", imagePath);

            try
            {
                if (token.IsCancellationRequested)
                {
                     _logger.LogDebug("Preload cancelled for {ImagePath} (token before Task.Run).", imagePath);
                    return;
                }

                BitmapImage? bitmap = await Task.Run(() => LoadBitmapImageFromFile(imagePath, token), token);

                if (bitmap != null && !token.IsCancellationRequested)
                {
                    lock (_imageCache)
                    {
                        _imageCache[imagePath] = bitmap;
                        _logger.LogDebug("Image {ImagePath} preloaded and added to cache.", imagePath);
                    }
                }
                 else if(token.IsCancellationRequested)
                {
                    _logger.LogInfo("Preload cancelled for {ImagePath} after Task.Run.", imagePath);
                }
            }
            catch (Exception ex_ensure) when (!(ex_ensure is OperationCanceledException))
            {
                _logger.LogWarning(ex_ensure.Message, "Exception during EnsureImageLoadedAsync for path {ImagePath}.", imagePath);
            }
            finally
            {
                lock (_currentlyPreloading)
                {
                    _currentlyPreloading.Remove(imagePath);
                }
            }
        }

        private void LeftArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _logger.LogDebug("Navigating left from index {CurrentIndex}", _currentIndex);
                LoadAndDisplayImage(_currentIndex - 1);
            }
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                _logger.LogDebug("Navigating right from index {CurrentIndex}", _currentIndex);
                LoadAndDisplayImage(_currentIndex + 1);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_settings == null) return;

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                if (!(_settings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows)))
                {
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Left)
                {
                    LeftArrow_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.Right)
                {
                    RightArrow_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("ImageTabControl unloading. Cancelling preload tasks and clearing cache.");
            if (_preloadCts != null)
            {
                _preloadCts.Cancel();
                _preloadCts.Dispose();
            }
            _imageCache.Clear();
            lock (_currentlyPreloading)
            {
                _currentlyPreloading.Clear();
            }
            DisplayedImage.Source = null;
            AppSettingsService.SettingsChanged -= HandleAppSettingsChanged;
            _logger.LogInfo("ImageTabControl unloaded.");
        }

        private void HandleAppSettingsChanged(object? sender, EventArgs e)
        {
            _logger.LogInfo("App settings changed, reloading ReaderModule settings for ImageTabControl.");
            _settings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());

            if (Dispatcher.CheckAccess())
            {
                ApplyNavigationSettings();
            }
            else
            {
                Dispatcher.Invoke(() => ApplyNavigationSettings());
            }
        }
    }
}

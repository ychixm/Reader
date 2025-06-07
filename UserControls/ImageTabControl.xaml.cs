using System;
using System.Collections.Generic;
// using System.Diagnostics; // Will be removed
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
using Reader.Business;

namespace Reader.UserControls
{
    /// <summary>
    /// Interaction logic for ImageTabControl.xaml
    /// </summary>
    public partial class ImageTabControl : UserControl
    {
        private AppSettings _settings;
        private readonly List<string> _imagePaths;
        private int _currentIndex;

        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private readonly HashSet<string> _currentlyPreloading = new HashSet<string>();
        private CancellationTokenSource _preloadCts = new CancellationTokenSource();
        private const int PreloadNextCount = 2;
        private const int PreloadPrevCount = 1;

        private static BitmapImage? _errorPlaceholderImage;

        private static void EnsureErrorPlaceholderLoaded()
        {
            if (_errorPlaceholderImage == null)
            {
                try
                {
                    string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ressources", "NoImage.png");
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
                    // System.Diagnostics.Debug.WriteLine($"Failed to load error placeholder image for ImageTabControl: {ex.Message}");
                }
            }
        }

        // DisplayedImage_MouseDown method removed as requested

        public void Grid_Overall_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Ignore clicks on the navigation buttons themselves
            if (e.OriginalSource == LeftArrow || e.OriginalSource == RightArrow)
            {
                return;
            }

            if (!(_settings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick)))
            {
                return;
            }

            if (_imagePaths == null || _imagePaths.Count == 0 || DisplayedImage.Source == _errorPlaceholderImage)
            {
                return;
            }

            Point position = e.GetPosition(this); // 'this' is the UserControl
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

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageTabControl"/> class.
        /// Displays images from the provided paths and enables navigation, caching, and preloading.
        /// </summary>
        /// <param name="imagePaths">A list of absolute string paths to the images to be displayed.</param>
        /// <exception cref="ArgumentNullException">Thrown if imagePaths is null.</exception>
        public ImageTabControl(List<string> imagePaths)
        {
            InitializeComponent();
            EnsureErrorPlaceholderLoaded();

            _settings = AppSettingsService.LoadAppSettings(); // Load settings

            _imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
            _preloadCts = new CancellationTokenSource();

            if (_imagePaths.Count == 0)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                DisplayedImage.Source = _errorPlaceholderImage;
                return;
            }

            _currentIndex = 0;
            LoadAndDisplayImage(_currentIndex);

            this.Focusable = true;
            this.Focus();

            ApplyNavigationSettings(); // Apply settings
        }

        private void ApplyNavigationSettings()
        {
            if (_settings == null)
            {
                // Fallback or log if settings are unexpectedly null
                // For robustness, one might load default AppSettings here
                // or ensure _settings is initialized to a default new AppSettings()
                // if LoadAppSettings could return null (though current AppSettingsService returns new AppSettings()).
                return;
            }

            bool showButtons = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);
            Visibility buttonVisibility = showButtons ? Visibility.Visible : Visibility.Collapsed;

            LeftArrow.Visibility = buttonVisibility;
            RightArrow.Visibility = buttonVisibility;
        }

        private static BitmapImage? LoadBitmapImageFromFile(string imagePath, CancellationToken token)
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
            catch (Exception ex) when (!(ex is OperationCanceledException || ex is ArgumentException ))
            {
                // System.Diagnostics.Debug.WriteLine($"Error loading BitmapImage from file {imagePath}: {ex.Message}");
                return null;
            }
        }

        private async void LoadAndDisplayImage(int index)
        {
            if (index < 0 || index >= _imagePaths.Count)
            {
                DisplayedImage.Source = _errorPlaceholderImage;
                LoadingIndicator.Visibility = Visibility.Collapsed;
                return;
            }
            _currentIndex = index;
            string imagePath = _imagePaths[index];

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
                // Image is in cache
            }
            else
            {
                try
                {
                    if (currentToken.IsCancellationRequested)
                    {
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
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    bitmapToShow = null;
                }
            }

            if (currentToken.IsCancellationRequested)
            {
                DisplayedImage.Source = _errorPlaceholderImage;
                LoadingIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            if (bitmapToShow != null)
            {
                DisplayedImage.Source = bitmapToShow;
            }
            else
            {
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
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {

            }
        }

        private async Task EnsureImageLoadedAsync(string imagePath, CancellationToken token)
        {
            if (token.IsCancellationRequested || _imageCache.ContainsKey(imagePath)) return;

            bool shouldLoad = false;
            lock (_currentlyPreloading)
            {
                if (!_currentlyPreloading.Contains(imagePath))
                {
                    _currentlyPreloading.Add(imagePath);
                    shouldLoad = true;
                }
            }

            if (!shouldLoad) return;

            try
            {
                if (token.IsCancellationRequested) return;

                BitmapImage? bitmap = await Task.Run(() => LoadBitmapImageFromFile(imagePath, token), token);

                if (bitmap != null && !token.IsCancellationRequested)
                {
                    lock (_imageCache)
                    {
                        _imageCache[imagePath] = bitmap;
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
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
                LoadAndDisplayImage(_currentIndex - 1);
            }
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                LoadAndDisplayImage(_currentIndex + 1);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                if (!(_settings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows)))
                {
                    e.Handled = true; // Optional: Mark as handled to prevent further processing
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
        }
    }
}

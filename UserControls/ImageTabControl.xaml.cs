using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Reader.UserControls
{
    /// <summary>
    /// Interaction logic for ImageTabControl.xaml
    /// </summary>
    public partial class ImageTabControl : UserControl
    {
        private readonly List<string> _imagePaths;
        private int _currentIndex;

        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private readonly HashSet<string> _currentlyPreloading = new HashSet<string>();
        private CancellationTokenSource _preloadCts = new CancellationTokenSource();
        private const int PreloadNextCount = 2; // Number of next images to preload
        private const int PreloadPrevCount = 1; // Number of previous images to preload

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
                    bmp.CacheOption = BitmapCacheOption.OnLoad; // Load it fully
                    bmp.EndInit();
                    bmp.Freeze(); // Make it shareable
                    _errorPlaceholderImage = bmp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load error placeholder image for ImageTabControl: {ex.Message}");
                    // _errorPlaceholderImage will remain null if loading fails
                }
            }
        }

        public ImageTabControl(List<string> imagePaths)
        {
            InitializeComponent();
            EnsureErrorPlaceholderLoaded(); // Call the helper

            _imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
            _preloadCts = new CancellationTokenSource();

            if (_imagePaths.Count == 0)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                DisplayedImage.Source = _errorPlaceholderImage; // Show placeholder if no images
                return;
            }

            _currentIndex = 0;
            LoadAndDisplayImage(_currentIndex);

            this.Focusable = true;
            this.Focus();
        }

        private async void LoadAndDisplayImage(int index)
        {
            if (index < 0 || index >= _imagePaths.Count)
            {
                DisplayedImage.Source = _errorPlaceholderImage; // Index out of bounds
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

                    bitmapToShow = await Task.Run(async () => {
                        if (currentToken.IsCancellationRequested) return null;
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(imagePath);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.CreateOptions = BitmapCreateOptions.None;
                        bmp.EndInit();
                        bmp.Freeze();
                        return currentToken.IsCancellationRequested ? null : bmp;
                    }, currentToken);

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
                    Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
                    bitmapToShow = null; // Ensure it's null on error
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
                DisplayedImage.Source = _errorPlaceholderImage; // Use the placeholder
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
                Debug.WriteLine($"Error during preloading group: {ex.Message}");
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

                BitmapImage? bitmap = await Task.Run(async () => {
                    if (token.IsCancellationRequested) return null;
                    BitmapImage loadedBitmap = new BitmapImage();
                    loadedBitmap.BeginInit();
                    loadedBitmap.UriSource = new Uri(imagePath);
                    loadedBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    loadedBitmap.CreateOptions = BitmapCreateOptions.None;
                    loadedBitmap.EndInit();
                    loadedBitmap.Freeze();
                    return token.IsCancellationRequested ? null : loadedBitmap;
                }, token);

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
                Debug.WriteLine($"Failed to preload image {imagePath}: {ex.Message}");
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

            if (e.Key == Key.Left)
            {
                LeftArrow_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Right)
            {
                RightArrow_Click(this, new RoutedEventArgs());
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

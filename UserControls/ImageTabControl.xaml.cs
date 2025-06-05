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

        public ImageTabControl(List<string> imagePaths)
        {
            InitializeComponent();
            _imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
            _preloadCts = new CancellationTokenSource(); // Initialize CTS

            if (_imagePaths.Count == 0)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                // Optionally display a "No images" message
                return;
            }

            _currentIndex = 0; // Set the initial index to 0 to load the first image
            LoadAndDisplayImage(_currentIndex); // This is async void

            // Set focus to the control to receive keyboard events
            this.Focusable = true;
            this.Focus();
        }

        private async void LoadAndDisplayImage(int index)
        {
            if (index < 0 || index >= _imagePaths.Count)
            {
                return;
            }
            _currentIndex = index;
            string imagePath = _imagePaths[index];

            // Cancel any ongoing preloads before starting a new one or loading the current image.
            // Create a new CTS for future preloads.
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
                // Image not in cache, load it
                try
                {
                    if (currentToken.IsCancellationRequested)
                    {
                        LoadingIndicator.Visibility = Visibility.Collapsed;
                        return;
                    }

                    bitmapToShow = await Task.Run(async () => { // Changed to async lambda
                        if (currentToken.IsCancellationRequested) return null;
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(imagePath);
                        bmp.CacheOption = BitmapCacheOption.OnLoad; // Load fully into memory
                        bmp.CreateOptions = BitmapCreateOptions.None; // Changed from IgnorePlaceHolder if that was used
                        bmp.EndInit();
                        bmp.Freeze(); // Allow cross-thread access
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
                    bitmapToShow = null;
                }
            }

            if (currentToken.IsCancellationRequested)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            if (bitmapToShow != null)
            {
                DisplayedImage.Source = bitmapToShow;
            }
            else
            {
                // Display a placeholder or leave blank if loading failed or was cancelled
                DisplayedImage.Source = null;
            }

            LoadingIndicator.Visibility = Visibility.Collapsed;

            if (!currentToken.IsCancellationRequested)
            {
                // Start preloading adjacent images with the current token
                _ = PreloadAdjacentImagesAsync(_currentIndex, currentToken);
            }
        }

        private async Task PreloadAdjacentImagesAsync(int currentIndex, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            List<Task> preloadTasks = new List<Task>();

            // Preload next images
            for (int i = 1; i <= PreloadNextCount; i++)
            {
                int nextIndex = currentIndex + i;
                if (nextIndex < _imagePaths.Count)
                {
                    preloadTasks.Add(EnsureImageLoadedAsync(_imagePaths[nextIndex], token));
                }
            }

            // Preload previous images
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
            catch (Exception ex) when (!(ex is OperationCanceledException)) // Don't log OperationCanceledException from Task.WhenAll
            {
                // Individual tasks already log their specific errors.
                // This catches aggregate exceptions or issues with Task.WhenAll itself.
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
                if (token.IsCancellationRequested) return; // Check token before Task.Run

                BitmapImage? bitmap = await Task.Run(async () => { // Changed to async lambda
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
                // LoadAndDisplayImage will handle CTS cancellation and creation
                LoadAndDisplayImage(_currentIndex - 1);
            }
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                // LoadAndDisplayImage will handle CTS cancellation and creation
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
            // DisplayedImage.CacheMode = null; // This property does not exist on Image control directly
            DisplayedImage.Source = null;

            // Force garbage collection to release memory asynchronously - this is generally not recommended.
            // Consider if this is truly necessary or if letting the GC manage memory is sufficient.
            // Task.Run(() =>
            // {
            //     GC.Collect();
            //     GC.WaitForPendingFinalizers();
            // });
        }
    }
}

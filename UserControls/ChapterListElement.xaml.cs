using System;
using System.Collections.Generic; // Keep for List<string>
using System.IO;
using System.Linq; // Needed for .Where and .ToList in OpenImageTab
using System.Threading.Tasks; // Needed for Task.Run
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reader.Models; // Keep for DirectoryData
// Removed Windows.UI.ViewManagement as UISettings is not defined in this snippet.
// If UISettings is a custom class in that namespace, it should be kept or its usage re-evaluated.
// For now, assuming SetLabelColorBasedOnTheme might be simplified or its dependency managed elsewhere if an error occurs.
// Re-adding for now as it was in original - if it causes build error, it's an external type.
using Windows.UI.ViewManagement;


namespace Reader.UserControls
{
    /// <summary>
    /// Represents a UI element that displays a single chapter, including its thumbnail and title.
    /// Handles user interaction for opening the chapter.
    /// </summary>
    public partial class ChapterListElement : UserControl
    {
        private static bool _isDarkModeInitialized = false;
        private static bool _cachedIsDarkMode;

        private DirectoryData _directory { get; } // Made getter-only
        private List<string>? _imagePaths = null;
        public static readonly int ImageHeight = 250;
        public static readonly double DesignHeight = 350.0;
        public static readonly double DesignWidth = 199.0;

        public static readonly Size DesignSize = new Size(DesignWidth, DesignHeight); // Add this line

        /// <summary>
        /// Initializes a new instance of the <see cref="ChapterListElement"/> class.
        /// </summary>
        /// <param name="directoryInfo">The directory information for the chapter.</param>
        public ChapterListElement(DirectoryInfo directoryInfo)
        {
            this.Width = DesignWidth;
            this.MinWidth = DesignWidth;
            this.Height = DesignHeight;
            this.MinHeight = DesignHeight;
            InitializeComponent();
            ChapterImage.MaxWidth = DesignWidth;
            ChapterImage.MaxHeight = ImageHeight; // This is int, MaxHeight is double, implicit conversion is fine.
            _directory = new DirectoryData(directoryInfo);

            this.MouseDown += ChapterListElement_MouseDown;
            this.MouseLeftButtonUp += ChapterListElement_MouseLeftButtonUp;
        }

        /// <summary>
        /// Sets the display text for the chapter's label.
        /// </summary>
        /// <param name="text">The text to display as the chapter title.</param>
        public void SetLabelText(string text)
        {
            ChapterLabel.Text = text;
            SetLabelColorBasedOnTheme();
        }

        /// <summary>
        /// Sets the image source for the chapter's thumbnail.
        /// Ensures the update is performed on the UI thread.
        /// </summary>
        /// <param name="imageSource">The BitmapImage to display as the thumbnail.</param>
        public void SetImageSource(BitmapImage imageSource)
        {
            if (ChapterImage.Dispatcher.CheckAccess())
            {
                ChapterImage.Source = imageSource;
            }
            else
            {
                ChapterImage.Dispatcher.BeginInvoke(new Action(() => ChapterImage.Source = imageSource));
            }
        }

        private void ChapterListElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenImageTab(true);
            }
        }

        private void ChapterListElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                OpenImageTab(false);
            }
        }

        private async void OpenImageTab(bool switchToTab)
        {
            // Assuming Application.Current.MainWindow is of type MainWindow
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                if (_imagePaths == null)
                {
                    _imagePaths = await Task.Run(() => Directory.EnumerateFiles(_directory.DirectoryInfo.FullName)
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        .ToList());
                }

                if (_imagePaths != null)
                {
                    mainWindow.AddImageTab(_directory.DirectoryInfo.FullName, _imagePaths, switchToTab);
                }
            }
        }

        private static bool GetCurrentSystemIsDarkMode()
        {
            if (!_isDarkModeInitialized)
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                _cachedIsDarkMode = backgroundColor.R < 128 && backgroundColor.G < 128 && backgroundColor.B < 128;
                _isDarkModeInitialized = true; // Set flag after initialization
            }
            return _cachedIsDarkMode;
        }

        private void SetLabelColorBasedOnTheme()
        {
            bool isDarkMode = GetCurrentSystemIsDarkMode(); // Use the new helper

            if (isDarkMode)
            {
                ChapterLabel.Style = (Style)Application.Current.Resources["DarkModeTextBlockStyle"];
            }
            else
            {
                ChapterLabel.Style = (Style)Application.Current.Resources["LightModeTextBlockStyle"];
            }
        }
    }
}
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media; // Added for Typeface, FormattedText
using Reader.Models;
using Utils;
using Utils.Wpf; // Added for TextBlockUtils


namespace Reader.UserControls
{
    /// <summary>
    /// Represents a UI element that displays a single chapter, including its thumbnail and title.
    /// Handles user interaction for opening the chapter.
    /// </summary>
    public partial class ChapterListElement : UserControl
    {
        private string _originalChapterText = string.Empty; // Added this line
        public event EventHandler<ChapterOpenRequestedEventArgs>? ChapterOpenRequested;

        private DirectoryData _directory { get; } // Made getter-only
        public System.IO.DirectoryInfo ChapterDirectory => _directory.DirectoryInfo;
        private List<string>? _imagePaths = null;
        public static readonly int ImageHeight = 250;
        public static readonly double DesignHeight = 350.0;
        public static readonly double DesignWidth = 199.0;

        public static readonly Size DesignSize = new Size(DesignWidth, DesignHeight);

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
            ChapterLabel.SizeChanged += ChapterLabel_SizeChanged; // Add this line
            ChapterImage.MaxWidth = DesignWidth;
            ChapterImage.MaxHeight = ImageHeight;
            _directory = new DirectoryData(directoryInfo);

            this.MouseDown += ChapterListElement_MouseDown;
            this.MouseLeftButtonUp += ChapterListElement_MouseLeftButtonUp;
        }

        private void ChapterLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TextBlockUtils.ApplyTruncationWithEllipsis(this.ChapterLabel, _originalChapterText);
        }

        /// <summary>
        /// Sets the display text for the chapter's label.
        /// </summary>
        /// <param name="text">The text to display as the chapter title.</param>
        public void SetLabelText(string text)
        {
            _originalChapterText = text;
            TextBlockUtils.ApplyTruncationWithEllipsis(this.ChapterLabel, _originalChapterText);
        }

        // UpdateChapterLabelText has been removed and its functionality moved to TextBlockUtils.ApplyTruncationWithEllipsis

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

            if (_imagePaths != null && _imagePaths.Count != 0) // Ensure there are images before raising event
            {
                var args = new ChapterOpenRequestedEventArgs(_directory.DirectoryInfo.FullName, _imagePaths, switchToTab);
                ChapterOpenRequested?.Invoke(this, args);
            }
        }
    }
}
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media; // Added for Typeface, FormattedText
using Reader.Models;
using Utils;


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
            UpdateChapterLabelText();
        }

        /// <summary>
        /// Sets the display text for the chapter's label.
        /// </summary>
        /// <param name="text">The text to display as the chapter title.</param>
        public void SetLabelText(string text)
        {
            _originalChapterText = text;
            UpdateChapterLabelText();
        }

        private void UpdateChapterLabelText()
        {
            if (string.IsNullOrEmpty(_originalChapterText) || ChapterLabel.ActualHeight == 0)
            {
                ChapterLabel.Text = _originalChapterText;
                return;
            }

            // Use a copy of ChapterLabel's properties for FormattedText
            var typeface = new System.Windows.Media.Typeface(
                ChapterLabel.FontFamily,
                ChapterLabel.FontStyle,
                ChapterLabel.FontWeight,
                ChapterLabel.FontStretch);

            // Estimate line height if not directly available or to be more precise
            double estimatedLineHeight = ChapterLabel.FontSize * typeface.FontFamily.LineSpacing;
            if (estimatedLineHeight <= 0) estimatedLineHeight = ChapterLabel.FontSize * 1.2; // Fallback

            double availableHeight = ChapterLabel.ActualHeight;
            // ChapterLabel has a fixed Height="100", so ActualHeight should be 100 unless layout forces otherwise.
            // Using ActualHeight is more robust.

            if (availableHeight <= 0)
            {
                ChapterLabel.Text = _originalChapterText; // Cannot calculate if no height
                return;
            }

            int maxLines = (int)Math.Max(1, Math.Floor(availableHeight / estimatedLineHeight));

            System.Windows.Media.FormattedText formattedText = new System.Windows.Media.FormattedText(
                _originalChapterText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                ChapterLabel.FontSize,
                ChapterLabel.Foreground,
                System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);

            formattedText.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width; // Use ActualWidth if available
            formattedText.MaxTextHeight = availableHeight; // Max height for the text
            formattedText.Trimming = TextTrimming.None; // We do our own trimming for the last line

            if (formattedText.Height > availableHeight || formattedText.LineCount > maxLines)
            {
                string ellipsis = "...";
                // string currentText = string.Empty; // This variable is not used
                string textToDisplay = _originalChapterText;

                // Iterate backwards to find suitable truncation point
                for (int i = _originalChapterText.Length - 1; i >= 0; i--)
                {
                    string prospectiveText = _originalChapterText.Substring(0, i) + ellipsis;
                    formattedText = new System.Windows.Media.FormattedText(
                        prospectiveText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        typeface,
                        ChapterLabel.FontSize,
                        ChapterLabel.Foreground,
                        System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    formattedText.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;

                    // Check if this prospective text (with ellipsis) fits
                    if (formattedText.Height <= availableHeight && formattedText.LineCount <= maxLines)
                    {
                        textToDisplay = prospectiveText;
                        break;
                    }
                    // If even a single char + ellipsis doesn't fit, then just put ellipsis or first few chars
                    if (i == 0) {
                        // Try to fit just the ellipsis or a very short string
                        string minimalText = ellipsis;
                         formattedText = new System.Windows.Media.FormattedText(
                            minimalText,
                            System.Globalization.CultureInfo.CurrentCulture,
                            System.Windows.FlowDirection.LeftToRight,
                            typeface,
                            ChapterLabel.FontSize,
                            ChapterLabel.Foreground,
                            System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        formattedText.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;
                        if(formattedText.Height <= availableHeight && formattedText.LineCount <= maxLines) {
                             textToDisplay = minimalText;
                        } else {
                            // Fallback: try to show just the beginning of the original text without ellipsis if ellipsis itself is too big
                            // This part might need more refinement based on desired behavior for extremely small spaces
                            string emergencyText = _originalChapterText.Substring(0, Math.Min(_originalChapterText.Length, 5)); // show first 5 chars
                             formattedText = new System.Windows.Media.FormattedText(
                                emergencyText,
                                System.Globalization.CultureInfo.CurrentCulture,
                                System.Windows.FlowDirection.LeftToRight,
                                typeface,
                                ChapterLabel.FontSize,
                                ChapterLabel.Foreground,
                                System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            formattedText.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;
                            if(formattedText.Height <= availableHeight && formattedText.LineCount <= maxLines) {
                               textToDisplay = emergencyText;
                            } else {
                               textToDisplay = ""; // Nothing fits
                            }
                        }
                        break;
                    }
                }
                ChapterLabel.Text = textToDisplay;
            }
            else
            {
                ChapterLabel.Text = _originalChapterText;
            }
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
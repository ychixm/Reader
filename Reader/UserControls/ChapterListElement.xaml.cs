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
            // Ensure estimatedLineHeight is positive and not excessively small
            if (estimatedLineHeight <= 0.001) estimatedLineHeight = ChapterLabel.FontSize * 1.2; // Fallback if linespacing is 0 or too small
            if (estimatedLineHeight <= 0.001) estimatedLineHeight = 12; // Absolute fallback if FontSize is also 0

            double availableHeight = ChapterLabel.ActualHeight;
            // ChapterLabel has a fixed Height="100", so ActualHeight should be 100 unless layout forces otherwise.
            // Using ActualHeight is more robust.

            if (availableHeight <= 0)
            {
                ChapterLabel.Text = _originalChapterText; // Cannot calculate if no height
                return;
            }

            // Calculate maxLines, ensuring estimatedLineHeight is usable
            int maxLines = (int)Math.Max(1, Math.Floor(availableHeight / estimatedLineHeight));
            if (estimatedLineHeight <= 0.001) maxLines = 1; // If line height is still effectively zero, allow only 1 line.

            System.Windows.Media.FormattedText ftOriginal = new System.Windows.Media.FormattedText(
                _originalChapterText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                ChapterLabel.FontSize,
                ChapterLabel.Foreground,
                System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);

            ftOriginal.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width; // Use ActualWidth if available
            ftOriginal.MaxTextHeight = availableHeight; // Max height for the text
            ftOriginal.Trimming = TextTrimming.None; // We do our own trimming for the last line

            double occupiedLinesForOriginalText = estimatedLineHeight > 0.001 ? Math.Ceiling(ftOriginal.Height / estimatedLineHeight) : (maxLines + 1.0);
            if (ftOriginal.Height > availableHeight || occupiedLinesForOriginalText > maxLines)
            {
                string ellipsis = "...";
                string textToDisplay = _originalChapterText;

                // Iterate backwards to find suitable truncation point
                for (int i = _originalChapterText.Length - 1; i >= 0; i--)
                {
                    string prospectiveText = _originalChapterText.Substring(0, i) + ellipsis;
                    System.Windows.Media.FormattedText ftProspective = new System.Windows.Media.FormattedText(
                        prospectiveText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        typeface,
                        ChapterLabel.FontSize,
                        ChapterLabel.Foreground,
                        System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    ftProspective.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;

                    double currentProspectiveLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftProspective.Height / estimatedLineHeight) : (maxLines + 1.0);
                    if (ftProspective.Height <= availableHeight && currentProspectiveLines <= maxLines)
                    {
                        textToDisplay = prospectiveText;
                        break;
                    }

                    if (i == 0) { // If even a single char + ellipsis doesn't fit
                        string minimalText = ellipsis;
                        System.Windows.Media.FormattedText ftMinimal = new System.Windows.Media.FormattedText(
                            minimalText,
                            System.Globalization.CultureInfo.CurrentCulture,
                            System.Windows.FlowDirection.LeftToRight,
                            typeface,
                            ChapterLabel.FontSize,
                            ChapterLabel.Foreground,
                            System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        ftMinimal.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;
                        double minimalLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftMinimal.Height / estimatedLineHeight) : (maxLines + 1.0);

                        if(ftMinimal.Height <= availableHeight && minimalLines <= maxLines) {
                             textToDisplay = minimalText;
                        } else {
                            string emergencyText = _originalChapterText.Substring(0, Math.Min(_originalChapterText.Length, 5));
                            System.Windows.Media.FormattedText ftEmergency = new System.Windows.Media.FormattedText(
                                emergencyText,
                                System.Globalization.CultureInfo.CurrentCulture,
                                System.Windows.FlowDirection.LeftToRight,
                                typeface,
                                ChapterLabel.FontSize,
                                ChapterLabel.Foreground,
                                System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            ftEmergency.MaxTextWidth = ChapterLabel.ActualWidth > 0 ? ChapterLabel.ActualWidth : ChapterLabel.Width;
                            double emergencyLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftEmergency.Height / estimatedLineHeight) : (maxLines + 1.0);

                            if(ftEmergency.Height <= availableHeight && emergencyLines <= maxLines) {
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
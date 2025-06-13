using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Utils.Wpf // Assuming a namespace, adjust if Utils project has a different default
{
    public static class TextBlockUtils
    {
        public static void ApplyTruncationWithEllipsis(TextBlock textBlock, string originalText)
        {
            if (textBlock == null) return;

            if (string.IsNullOrEmpty(originalText) || textBlock.ActualHeight == 0 || textBlock.ActualWidth == 0)
            {
                textBlock.Text = originalText;
                return;
            }

            var typeface = new Typeface(
                textBlock.FontFamily,
                textBlock.FontStyle,
                textBlock.FontWeight,
                textBlock.FontStretch);

            double estimatedLineHeight = textBlock.FontSize * typeface.FontFamily.LineSpacing;
            if (estimatedLineHeight <= 0.001 && typeface.FontFamily.LineSpacing == 0) // Check if LineSpacing was the cause
            {
                 // Common heuristic if LineSpacing is 0 (e.g. for some fonts or if not set)
                estimatedLineHeight = textBlock.FontSize * 1.2;
            }
            if (estimatedLineHeight <= 0.001) // Fallback if FontSize is also 0 or negative
            {
                estimatedLineHeight = 12 * 1.2; // Absolute fallback based on a common default FontSize
            }


            double availableHeight = textBlock.ActualHeight;
            int maxLines = (int)Math.Max(1, Math.Floor(availableHeight / estimatedLineHeight));
             if (estimatedLineHeight <= 0.001) // Safety for maxLines if estimatedLineHeight is still bad
            {
                maxLines = 1;
            }


            FormattedText ftOriginal = new FormattedText(
                originalText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                textBlock.FontSize <= 0 ? 12 : textBlock.FontSize, // Use a fallback for FontSize if 0 or less
                textBlock.Foreground,
                VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

            ftOriginal.MaxTextWidth = textBlock.ActualWidth;
            ftOriginal.MaxTextHeight = availableHeight; // Not strictly necessary here as we calculate lines, but good for reference
            ftOriginal.Trimming = TextTrimming.None; // We handle trimming

            double occupiedLinesForOriginalText = estimatedLineHeight > 0.001 ? Math.Ceiling(ftOriginal.Height / estimatedLineHeight) : (maxLines + 1.0);

            if (ftOriginal.Height > availableHeight || occupiedLinesForOriginalText > maxLines)
            {
                string ellipsis = "...";
                string textToDisplay = originalText; // Default to original if something goes wrong

                for (int i = originalText.Length - 1; i >= 0; i--)
                {
                    string prospectiveText = originalText.Substring(0, i) + ellipsis;
                    FormattedText ftProspective = new FormattedText(
                        prospectiveText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        textBlock.FontSize <= 0 ? 12 : textBlock.FontSize,
                        textBlock.Foreground,
                        VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

                    ftProspective.MaxTextWidth = textBlock.ActualWidth;

                    double currentProspectiveLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftProspective.Height / estimatedLineHeight) : (maxLines + 1.0);

                    if (ftProspective.Height <= availableHeight && currentProspectiveLines <= maxLines)
                    {
                        textToDisplay = prospectiveText;
                        break;
                    }

                    if (i == 0) // Only ellipsis or very short text left to try
                    {
                        string minimalText = ellipsis;
                        FormattedText ftMinimal = new FormattedText(
                            minimalText,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            textBlock.FontSize <= 0 ? 12 : textBlock.FontSize,
                            textBlock.Foreground,
                            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

                        ftMinimal.MaxTextWidth = textBlock.ActualWidth;
                        double minimalLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftMinimal.Height / estimatedLineHeight) : (maxLines + 1.0);

                        if (ftMinimal.Height <= availableHeight && minimalLines <= maxLines)
                        {
                            textToDisplay = minimalText;
                        }
                        else
                        {
                            // Fallback: Try to show just the beginning of the original text if ellipsis itself is too big
                            // This part might need more refinement based on desired behavior for extremely small spaces
                            string emergencyText = originalText.Length > 5 ? originalText.Substring(0, 5) : originalText;
                            FormattedText ftEmergency = new FormattedText(
                                emergencyText,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                textBlock.FontSize <= 0 ? 12 : textBlock.FontSize,
                                textBlock.Foreground,
                                VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

                            ftEmergency.MaxTextWidth = textBlock.ActualWidth;
                            double emergencyLines = estimatedLineHeight > 0.001 ? Math.Ceiling(ftEmergency.Height / estimatedLineHeight) : (maxLines + 1.0);
                            if(ftEmergency.Height <= availableHeight && emergencyLines <= maxLines)
                            {
                               textToDisplay = emergencyText;
                            } else {
                               textToDisplay = ""; // Give up, nothing fits
                            }
                        }
                        break;
                    }
                }
                textBlock.Text = textToDisplay;
            }
            else
            {
                textBlock.Text = originalText;
            }
        }
    }
}

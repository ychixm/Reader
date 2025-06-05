using System; // Added for Action
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reader.Models;
using Windows.UI.ViewManagement;

namespace Reader.UserControls
{
    /// <summary>
    /// Interaction logic for ChapterListElement.xaml
    /// </summary>
    public partial class ChapterListElement : UserControl
    {
        private DirectoryData _directory { get; set; }
        private List<string>? _imagePaths = null; // Made nullable and initialized to null
        public static readonly int ImageHeight = 250;
        public static readonly double DesignHeight = 350.0;
        public static readonly double DesignWidth = 199.0;


        public ChapterListElement(DirectoryInfo directoryInfo)
        {
            this.Width = DesignWidth;
            this.MinWidth = DesignWidth;
            this.Height = DesignHeight;
            this.MinHeight = DesignHeight;
            InitializeComponent();
            ChapterImage.MaxWidth = DesignWidth;
            ChapterImage.MaxHeight = ImageHeight;
            _directory = new DirectoryData(directoryInfo);

            this.MouseDown += ChapterListElement_MouseDown;
            this.MouseLeftButtonUp += ChapterListElement_MouseLeftButtonUp;

        }


        public void SetLabelText(string text)
        {
            ChapterLabel.Text = text;
            SetLabelColorBasedOnTheme();
        }

        public void SetImageSource(BitmapImage imageSource)
        {
            if (ChapterImage.Dispatcher.CheckAccess())
            {
                ChapterImage.Source = imageSource;
            }
            else
            {
                // Use BeginInvoke for fire-and-forget update to UI thread
                ChapterImage.Dispatcher.BeginInvoke(new Action(() => ChapterImage.Source = imageSource));
            }
        }

        private void ChapterListElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenImageTab(true); // Create tab and switch to it
            }
        }

        private void ChapterListElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                OpenImageTab(false); // Create tab without switching
            }
        }

        private async void OpenImageTab(bool switchToTab)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                if (_imagePaths == null) // Check if already populated
                {
                    _imagePaths = await Task.Run(() => Directory.EnumerateFiles(_directory.DirectoryInfo.FullName)
                        .Where(f => f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".bmp", System.StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".gif", System.StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase))
                        .ToList());
                }

                if (_imagePaths != null) // Add null check for safety
                {
                    mainWindow.AddImageTab(_directory.DirectoryInfo.FullName, _imagePaths, switchToTab);
                }
            }
        }

        private void SetLabelColorBasedOnTheme()
        {
            var uiSettings = new UISettings();
            var backgroundColor = uiSettings.GetColorValue(UIColorType.Background);
            var isDarkMode = backgroundColor.R < 128 && backgroundColor.G < 128 && backgroundColor.B < 128;

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
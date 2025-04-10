using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Display.Core;

namespace Reader.UserControls
{
    /// <summary>
    /// Interaction logic for ImageTabControl.xaml
    /// </summary>
    public partial class ImageTabControl : UserControl
    {
        private readonly List<string> _imagePaths;
        private int _currentIndex;

        public ImageTabControl(List<string> imagePaths)
        {
            InitializeComponent();
            _imagePaths = imagePaths;
            _currentIndex = 0; // Set the initial index to 0 to load the first image
            LoadAndDisplayImage(_currentIndex);

            // Set focus to the control to receive keyboard events
            this.Focusable = true;
            this.Focus();
        }

        private void LoadAndDisplayImage(int index)
        {
            if (index >= 0 && index < _imagePaths.Count)
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_imagePaths[index]);
                bitmap.EndInit();
                DisplayedImage.Dispatcher.BeginInvoke(() =>
                {
                    DisplayedImage.Source = bitmap;
                });
            }
        }

        private void LeftArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                LoadAndDisplayImage(_currentIndex);
            }
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                _currentIndex++;
                LoadAndDisplayImage(_currentIndex);
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
            // Clear the image paths and set the displayed image to null
            _imagePaths.Clear();
            DisplayedImage.CacheMode = null; // Clear the cache mode to release memory  
            DisplayedImage.Source = null;

            // Force garbage collection to release memory asynchronously
            Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            });
        }
    }
}

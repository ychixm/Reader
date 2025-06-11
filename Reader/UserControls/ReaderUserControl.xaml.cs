using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reader.Business;
using Reader.UserControls; // This using might seem redundant if the namespace is Reader.UserControls, but it's good for clarity with other UserControls
using Reader.Models;
using Utils;
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.Runtime.CompilerServices; // Required for CallerMemberName
using System; // Added for System.Random and System.StringComparer, System.HashSet, System.Exception
using System.Linq; // Added for Enumerable.OfType and Enumerable.Any
using System.Collections.Generic; // Added for List and HashSet
using System.Threading.Tasks; // Added for Task

namespace Reader.UserControls
{
    /// <summary>
    /// Interaction logic for ReaderUserControl.xaml
    /// </summary>
    public partial class ReaderUserControl : UserControl, INotifyPropertyChanged
    {
        private AppSettings _settings; // Initialized in constructor.
        private TabOverflowManager? _tabOverflowManager; // Initialized in MainTabControl_Loaded

        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };

        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png"; // Make sure this path is accessible from the new location or adjust as needed. For now, assume it's relative to app execution dir.

        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;

        public ReaderUserControl()
        {
            InitializeComponent();
            _settings = AppSettingsService.LoadAppSettings();
            LoadNavigationOptionStates();

            // Attach event handlers for navigation options
            // These names (e.g., KeyboardArrowsOption) are x:Name in XAML. Ensure they are accessible.
            // If XAML elements are not found, it might be due to loading sequence or access levels.
            // However, these are typically found after InitializeComponent.
            KeyboardArrowsOption.Checked += NavigationOption_Changed;
            KeyboardArrowsOption.Unchecked += NavigationOption_Changed;
            GridClickOption.Checked += NavigationOption_Changed;
            GridClickOption.Unchecked += NavigationOption_Changed;
            VisibleButtonsOption.Checked += NavigationOption_Changed;
            VisibleButtonsOption.Unchecked += NavigationOption_Changed;

            LoadChapterListAsync();
        }

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            ChapterListElement chapterListElement = new(directory)
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };
            chapterListElement.ChapterOpenRequested += HandleChapterOpenRequested;

            chapterListElement.SetLabelText(directory.Name);
            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

            Views.Add(chapterListElement);

            var imageSourceUri = await Task.Run(() => FileSystemHelpers.GetFirstFileByExtensions(directory, SupportedImageExtensions));

            if (imageSourceUri != null)
            {
                BitmapImage? finalThumbnail = await Task.Run(() => {
                    var (width, height) = FileSystemHelpers.GetImageDimensions(imageSourceUri.LocalPath);
                    BitmapImage thumbnail = new BitmapImage();
                    thumbnail.BeginInit();
                    thumbnail.UriSource = imageSourceUri;
                    if (width > height) { thumbnail.DecodePixelWidth = (int)ChapterListElement.DesignWidth; }
                    else { thumbnail.DecodePixelHeight = ChapterListElement.ImageHeight; }
                    thumbnail.CreateOptions = BitmapCreateOptions.None;
                    thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                    thumbnail.EndInit();
                    thumbnail.Freeze();
                    return thumbnail;
                });

                if (finalThumbnail != null)
                {
                    chapterListElement.SetImageSource(finalThumbnail);
                }
            }
        }

        private async void LoadChapterListAsync()
        {
            try
            {
                string effectivePath;
                AppSettings settings = AppSettingsService.LoadAppSettings();
                if (!string.IsNullOrEmpty(settings.DefaultPath))
                {
                    effectivePath = settings.DefaultPath;
                }
                else
                {
                    effectivePath = AppDomain.CurrentDomain.BaseDirectory;
                }

                List<DirectoryInfo> chapters = await Task.Run(() => FileSystemHelpers.GetDirectories(effectivePath));

                foreach (var directory in chapters)
                {
                    await ProcessChapterDirectoryAsync(directory);
                }
                MainTabHeaderTextBlock.Text += " (Loaded)";
            }
            catch (Exception)
            {
                // System.Diagnostics.Debug.WriteLine($"LoadChapterListAsync - An error occurred: {ex.Message}");
            }
        }

        public void AddImageTab(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            var existingTab = MainTabControl.Items.OfType<TabItem>()
                .FirstOrDefault(tab => tab.Tag is string path && path == directoryPath);

            if (existingTab != null)
            {
                if (switchToTab)
                {
                    MainTabControl.SelectedItem = existingTab;
                }
                return;
            }

            var imageTabControl = new ImageTabControl(imagePaths); // Assuming ImageTabControl is accessible
            string tabTitle = Path.GetFileName(directoryPath);

            if (tabTitle.Length > MaxTitleLength)
            {
                tabTitle = string.Concat(tabTitle.AsSpan(0, MaxTitleLength), "...");
            }

            var tabItem = new TabItem
            {
                Header = tabTitle,
                Content = imageTabControl,
                Tag = directoryPath
            };

            MainTabControl.Items.Add(tabItem);
            if (switchToTab)
            {
                MainTabControl.SelectedItem = tabItem;
            }
        }

        private void MainTabControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                var tabItem = WpfHelpers.FindParent<TabItem>((DependencyObject)e.OriginalSource);
                if (tabItem != null && tabItem != MainTab) // MainTab is x:Name in XAML
                {
                    MainTabControl.Items.Remove(tabItem);
                }
            }
        }

        private void MainTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            // The 'this' reference for TabOverflowManager context might need careful review.
            // If TabOverflowManager expects a Window or specific properties from BaseWindow, this could be an issue.
            // For now, we pass 'this' (MainUserControl instance).
            // The MenuItem parameters (ScrollbarModeMenuItem etc.) are x:Names from XAML.
            // _tabOverflowManager = new TabOverflowManager(MainTabControl, this, ScrollbarModeMenuItem, ArrowButtonsModeMenuItem, TabDropdownModeMenuItem);

            var tabListContextMenu = (ContextMenu)this.Resources["TabListContextMenu"];
            if (tabListContextMenu == null)
            {
                throw new InvalidOperationException("TabListContextMenu not found in UserControl resources.");
            }

            // MainTabHeaderTextBlock, ScrollbarModeMenuItem, ArrowButtonsModeMenuItem, TabDropdownModeMenuItem are x:Name defined in XAML for this UserControl
            _tabOverflowManager = new TabOverflowManager(
                MainTabControl,
                tabListContextMenu,
                MainTabHeaderTextBlock, // This is the x:Name of the TextBlock
                ScrollbarModeMenuItem,
                ArrowButtonsModeMenuItem,
                TabDropdownModeMenuItem
            );
            this.DataContext = this; // Set DataContext for bindings like {Binding CurrentTabOverflowMode}
        }

        public TabOverflowMode CurrentTabOverflowMode
        {
            get
            {
                return _tabOverflowManager != null ? _tabOverflowManager.CurrentTabOverflowMode : default;
            }
            set
            {
                if (_tabOverflowManager != null && _tabOverflowManager.CurrentTabOverflowMode != value)
                {
                    _tabOverflowManager.SetOverflowMode(value);
                    OnPropertyChanged();
                }
                else if (_tabOverflowManager == null && value != default)
                {
                    // Handle case where manager is not yet initialized.
                }
            }
        }

        private void LoadNavigationOptionStates()
        {
            _settings = AppSettingsService.LoadAppSettings();
            if (_settings == null) _settings = new AppSettings();

            KeyboardArrowsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows);
            GridClickOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick);
            VisibleButtonsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);
        }

        private void NavigationOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null)
            {
                _settings = AppSettingsService.LoadAppSettings();
                if (_settings == null) _settings = new AppSettings();
            }

            NavigationMethod currentMethods = NavigationMethod.None;
            if (KeyboardArrowsOption.IsChecked) currentMethods |= NavigationMethod.KeyboardArrows;
            if (GridClickOption.IsChecked) currentMethods |= NavigationMethod.GridClick;
            if (VisibleButtonsOption.IsChecked) currentMethods |= NavigationMethod.VisibleButtons;

            if (currentMethods == NavigationMethod.None)
            {
                if (sender is MenuItem menuItem)
                {
                    menuItem.IsChecked = true;
                }
                return;
            }

            _settings.EnabledNavigationMethods = currentMethods;
            AppSettingsService.SaveAppSettings(_settings);
        }

        private void SetOverflowMode_Scrollbar_Click(object sender, RoutedEventArgs e)
        {
            if (_tabOverflowManager != null)
            {
                _tabOverflowManager.SetOverflowMode(TabOverflowMode.Scrollbar);
                OnPropertyChanged(nameof(CurrentTabOverflowMode));
            }
        }

        private void SetOverflowMode_Arrows_Click(object sender, RoutedEventArgs e)
        {
            if (_tabOverflowManager != null)
            {
                _tabOverflowManager.SetOverflowMode(TabOverflowMode.ArrowButtons);
                OnPropertyChanged(nameof(CurrentTabOverflowMode));
            }
        }

        private void SetOverflowMode_Dropdown_Click(object sender, RoutedEventArgs e)
        {
            if (_tabOverflowManager != null)
            {
                _tabOverflowManager.SetOverflowMode(TabOverflowMode.TabDropdown);
                OnPropertyChanged(nameof(CurrentTabOverflowMode));
            }
        }

        private async Task OpenRandomChapter(bool switchToTab)
        {
            if (Views == null || !Views.Any())
            {
                // Consider how to show MessageBox from a UserControl.
                // Typically, it's fine, but if this control is hosted in a non-standard way, it might be an issue.
                MessageBox.Show("No chapters loaded to choose from.", "Random Chapter", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Random random = new Random();
            int randomIndex = random.Next(Views.Count);
            ChapterListElement selectedChapterElement = Views[randomIndex];

            DirectoryInfo chapterDirectoryInfo = selectedChapterElement.ChapterDirectory;

            if (chapterDirectoryInfo == null)
            {
                MessageBox.Show("Selected chapter element does not have directory information.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            List<string>? imagePaths = null;
            try
            {
                imagePaths = await Task.Run(() => Directory.EnumerateFiles(chapterDirectoryInfo.FullName)
                    .Where(f => SupportedImageExtensions.Contains(Path.GetExtension(f))) // More robust check using HashSet
                    .ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading images from directory {chapterDirectoryInfo.FullName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (imagePaths != null && imagePaths.Any()) // Changed from Count != 0 to Any()
            {
                AddImageTab(chapterDirectoryInfo.FullName, imagePaths, switchToTab);
            }
            else
            {
                MessageBox.Show($"No supported image files found in {chapterDirectoryInfo.FullName}.", "Random Chapter", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void RandomChapterButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenRandomChapter(true);
        }

        private async void RandomChapterButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                await OpenRandomChapter(false);
            }
        }

        private void HandleChapterOpenRequested(object? sender, ChapterOpenRequestedEventArgs e)
        {
            AddImageTab(e.DirectoryPath, e.ImagePaths, e.SwitchToTab);
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged; // Nullable for modern C#

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) // Nullable for modern C#
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

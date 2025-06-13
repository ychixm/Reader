using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added back
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reader.Business;
// using Reader.UserControls; // Optional, if no other UserControls from same namespace are used.
using Reader.Models; // Still needed for AppSettings, NavigationMethod etc.
using Utils.Controls; // Changed from Reader.Controls
using Utils.Models;   // Added for TabOverflowMode
using Utils; // For WpfHelpers
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reader.UserControls
{
    public partial class ReaderUserControl : UserControl, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private TabOverflowManagementControl? _tabOverflowManagementCtrl;

        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };
        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;
        private ChapterListElement? _randomChapterElement; // Field to hold the random chapter element

        public ReaderUserControl()
        {
            InitializeComponent();
            _settings = AppSettingsService.LoadAppSettings();
            _tabOverflowManagementCtrl = this.TabOverflowControl; // Name from XAML
            LoadNavigationOptionStates();

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
                // Create and add the "Random Chapter" element
                string randomChapterPlaceholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RandomChapterPlaceholder");
                Directory.CreateDirectory(randomChapterPlaceholderPath); // Ensure directory exists
                DirectoryInfo randomChapterDirInfo = new DirectoryInfo(randomChapterPlaceholderPath);

                _randomChapterElement = new ChapterListElement(randomChapterDirInfo)
                {
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1),
                    IsSpecialRandomElement = true // Mark this as the special random element
                };
                _randomChapterElement.SetLabelText("Random Chapter");
                // Image will use default placeholder logic in ChapterListElement or set explicitly if needed
                // string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
                // _randomChapterElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

                // Event handlers for the Random Chapter element
                _randomChapterElement.MouseLeftButtonUp += async (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        // No need to check IsSpecialRandomElement here as this handler is specific to _randomChapterElement
                        await OpenRandomChapter(true);
                    }
                };
                _randomChapterElement.MouseDown += async (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        // No need to check IsSpecialRandomElement here as this handler is specific to _randomChapterElement
                        await OpenRandomChapter(false);
                    }
                };

                Views.Insert(0, _randomChapterElement);

                string effectivePath;
                // Use _settings field which is loaded in constructor
                if (!string.IsNullOrEmpty(_settings.DefaultPath))
                {
                    effectivePath = _settings.DefaultPath;
                }
                else
                {
                    effectivePath = AppDomain.CurrentDomain.BaseDirectory;
                }

                List<DirectoryInfo> chapters = await Task.Run(() => FileSystemHelpers.GetDirectories(effectivePath));

                foreach (var directory in chapters)
                {
                    // Skip the placeholder directory if it's listed among chapters (it shouldn't be if DefaultPath is different)
                    if (directory.FullName.Equals(randomChapterDirInfo.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    await ProcessChapterDirectoryAsync(directory);
                }
                MainTabHeaderTextBlock.Text += " (Loaded)";
            }
            catch (Exception ex)
            {
                // Log error, e.g., using a logging framework or MessageBox
                MessageBox.Show($"Error loading chapter list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddImageTab(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            var existingTab = MainTabControl.Items.OfType<TabItem>()
                .FirstOrDefault(tab => tab.Tag is string path && path == directoryPath);

            if (existingTab != null)
            {
                if (switchToTab) MainTabControl.SelectedItem = existingTab;
                return;
            }

            var imageTabControl = new ImageTabControl(imagePaths);
            string tabTitle = Path.GetFileName(directoryPath);
            if (tabTitle.Length > MaxTitleLength) tabTitle = string.Concat(tabTitle.AsSpan(0, MaxTitleLength), "...");

            var tabItem = new TabItem { Header = tabTitle, Content = imageTabControl, Tag = directoryPath };
            MainTabControl.Items.Add(tabItem);
            if (switchToTab) MainTabControl.SelectedItem = tabItem;
        }

        private void MainTabControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                var tabItem = WpfHelpers.FindParent<TabItem>((DependencyObject)e.OriginalSource);
                if (tabItem != null && tabItem != MainTab) MainTabControl.Items.Remove(tabItem);
            }
        }

        private void MainTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_tabOverflowManagementCtrl != null)
            {
                Utils.Models.TabOverflowMode initialMode = Utils.Models.TabOverflowMode.Scrollbar; // Default
                if (_settings != null && !string.IsNullOrEmpty(_settings.DefaultTabOverflowMode))
                {
                    Enum.TryParse<Utils.Models.TabOverflowMode>(_settings.DefaultTabOverflowMode, out initialMode);
                }

                _tabOverflowManagementCtrl.InitializeManager(
                    MainTabControl,
                    MainTabHeaderTextBlock,
                    ScrollbarModeMenuItem,
                    ArrowButtonsModeMenuItem,
                    TabDropdownModeMenuItem,
                    initialMode
                );
                _tabOverflowManagementCtrl.ModeChanged += TabOverflowManagementCtrl_ModeChanged;
            }
            this.DataContext = this;
        }

        private void TabOverflowManagementCtrl_ModeChanged(Utils.Models.TabOverflowMode newMode)
        {
            if (_settings != null)
            {
                _settings.DefaultTabOverflowMode = newMode.ToString();
                AppSettingsService.SaveAppSettings(_settings);
            }
            OnPropertyChanged(nameof(CurrentTabOverflowMode));
        }

        public Utils.Models.TabOverflowMode CurrentTabOverflowMode
        {
            get => _tabOverflowManagementCtrl != null ? _tabOverflowManagementCtrl.CurrentTabOverflowMode : Utils.Models.TabOverflowMode.Scrollbar;
            set
            {
                if (_tabOverflowManagementCtrl != null && _tabOverflowManagementCtrl.CurrentTabOverflowMode != value)
                {
                    _tabOverflowManagementCtrl.SetOverflowMode(value);
                    // OnPropertyChanged() is called by TabOverflowManagementCtrl_ModeChanged via the event flow
                }
            }
        }

        private void LoadNavigationOptionStates()
        {
            if (_settings == null) _settings = new AppSettings(); // Should be loaded by constructor

            KeyboardArrowsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows);
            GridClickOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick);
            VisibleButtonsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);
        }

        private void NavigationOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null) _settings = AppSettingsService.LoadAppSettings(); // Ensure settings are loaded

            NavigationMethod currentMethods = NavigationMethod.None;
            if (KeyboardArrowsOption.IsChecked) currentMethods |= NavigationMethod.KeyboardArrows;
            if (GridClickOption.IsChecked) currentMethods |= NavigationMethod.GridClick;
            if (VisibleButtonsOption.IsChecked) currentMethods |= NavigationMethod.VisibleButtons;

            if (currentMethods == NavigationMethod.None && sender is MenuItem menuItem)
            {
                menuItem.IsChecked = true; // Prevent unchecking the last option
                return;
            }

            _settings.EnabledNavigationMethods = currentMethods;
            AppSettingsService.SaveAppSettings(_settings);
        }

        private void SetOverflowMode_Scrollbar_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManagementCtrl?.SetOverflowMode(Utils.Models.TabOverflowMode.Scrollbar);
        }

        private void SetOverflowMode_Arrows_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManagementCtrl?.SetOverflowMode(Utils.Models.TabOverflowMode.ArrowButtons);
        }

        private void SetOverflowMode_Dropdown_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManagementCtrl?.SetOverflowMode(Utils.Models.TabOverflowMode.TabDropdown);
        }

        private async Task OpenRandomChapter(bool switchToTab)
        {
            // Exclude the "Random Chapter" element itself from being chosen
            var actualChapterElements = Views.Where(v => v != _randomChapterElement).ToList();

            if (actualChapterElements == null || !actualChapterElements.Any())
            {
                MessageBox.Show("No actual chapters loaded to choose from.", "Random Chapter", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Random random = new Random();
            int randomIndex = random.Next(actualChapterElements.Count);
            ChapterListElement selectedChapterElement = actualChapterElements[randomIndex];
            DirectoryInfo chapterDirectoryInfo = selectedChapterElement.ChapterDirectory;

            if (chapterDirectoryInfo == null)
            {
                MessageBox.Show("Selected chapter element does not have directory information.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Check if the selected directory is the placeholder, which should not happen if filtered correctly
            if (chapterDirectoryInfo.Name == "RandomChapterPlaceholder")
            {
                MessageBox.Show("Cannot open the placeholder as a chapter.", "Random Chapter", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<string>? imagePaths = null;
            try
            {
                imagePaths = await Task.Run(() => Directory.EnumerateFiles(chapterDirectoryInfo.FullName)
                    .Where(f => SupportedImageExtensions.Contains(Path.GetExtension(f)))
                    .ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading images from directory {chapterDirectoryInfo.FullName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (imagePaths != null && imagePaths.Any())
            {
                AddImageTab(chapterDirectoryInfo.FullName, imagePaths, switchToTab);
            }
            else
            {
                MessageBox.Show($"No supported image files found in {chapterDirectoryInfo.FullName}.", "Random Chapter", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void HandleChapterOpenRequested(object? sender, ChapterOpenRequestedEventArgs e) => AddImageTab(e.DirectoryPath, e.ImagePaths, e.SwitchToTab);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

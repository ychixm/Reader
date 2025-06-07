using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For RepeatButton
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// Removed: using System.Xml.Linq;
using Reader.Business;
using Reader.UserControls;
using System;
using System.Threading.Tasks; // Added for Task
using System.Windows.Threading; // For DispatcherPriority
using System.ComponentModel; // For INotifyPropertyChanged
using System.Runtime.CompilerServices; // For CallerMemberName
using Reader.Models; // For TabOverflowMode
using Reader.Utils; // For WpfHelpers

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        private AppSettings _settings; // Initialized in constructor.
        private TabOverflowManager? _tabOverflowManager; // Initialized in MainTabControl_Loaded

        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };

        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";

        /// <summary>
        /// Gets the collection of ChapterListElement items to be displayed.
        /// This collection is bound to the ItemsControl in the XAML.
        /// </summary>
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;

        public MainWindow()
        {
            InitializeComponent();
            // this.DataContext = this;

            // Initialize _settings directly here as LoadNavigationOptionStates uses it.
            _settings = AppSettingsService.LoadAppSettings(); // Initialize _settings
            // LoadNavigationOptionStates will also call LoadAppSettings, but _settings needs to be non-null before that.
            // Or, ensure LoadNavigationOptionStates handles a potentially null _settings or is called after _settings is set.
            // The existing LoadNavigationOptionStates re-assigns _settings.

            // LoadPersistedTabOverflowMode(); // Moved to TabOverflowManager
            LoadNavigationOptionStates(); // Load and apply navigation states. This will set _settings.

            // Attach event handlers for navigation options
            KeyboardArrowsOption.Checked += NavigationOption_Changed;
            KeyboardArrowsOption.Unchecked += NavigationOption_Changed;
            GridClickOption.Checked += NavigationOption_Changed;
            GridClickOption.Unchecked += NavigationOption_Changed;
            VisibleButtonsOption.Checked += NavigationOption_Changed;
            VisibleButtonsOption.Unchecked += NavigationOption_Changed;

            LoadChapterListAsync(); // Existing method
        }

        // LoadPersistedTabOverflowMode MOVED to TabOverflowManager
        // SaveCurrentOverflowModeSetting MOVED to TabOverflowManager

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            ChapterListElement chapterListElement = new(directory)
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };
            chapterListElement.ChapterOpenRequested += HandleChapterOpenRequested; // Subscribe to the event

            chapterListElement.SetLabelText(directory.Name);
            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

            Views.Add(chapterListElement);

            var imageSourceUri = await Task.Run(() => FileSystemHelpers.GetFirstFileByExtensions(directory, SupportedImageExtensions)); // Changed to FileSystemHelpers

            if (imageSourceUri != null)
            {
                BitmapImage? finalThumbnail = await Task.Run(() => {
                    var (width, height) = FileSystemHelpers.GetImageDimensions(imageSourceUri.LocalPath); // Changed to FileSystemHelpers
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

                List<DirectoryInfo> chapters = await Task.Run(() => FileSystemHelpers.GetDirectories(effectivePath)); // Changed to FileSystemHelpers

                foreach (var directory in chapters)
                {
                    await ProcessChapterDirectoryAsync(directory);
                }
                MainTabHeaderTextBlock.Text += " (Loaded)";
            }
            catch (Exception) // CS0168: ex not used
            {
                // System.Diagnostics.Debug.WriteLine($"LoadChapterListAsync - An error occurred: {ex.Message}");
                // Optionally, update the UI to show an error message
                // Or handle exception more gracefully
            }
        }

        // Deleted ChapterListElement_Loaded method

        /// <summary>
        /// Adds a new tab for displaying images from a specified directory path,
        /// or selects an existing tab if one for the directory already exists.
        /// </summary>
        /// <param name="directoryPath">The full path to the directory containing images.</param>
        /// <param name="imagePaths">A list of full paths to the images within the directory.</param>
        /// <param name="switchToTab">True to select the tab after adding/finding it; false otherwise.</param>
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

            var imageTabControl = new ImageTabControl(imagePaths);
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
                if (tabItem != null && tabItem != MainTab)
                {
                    MainTabControl.Items.Remove(tabItem);
                }
            }
        }

        private void MainTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Instantiate TabOverflowManager. It will handle finding template parts and subscribing to events.
            _tabOverflowManager = new TabOverflowManager(MainTabControl, this, ScrollbarModeMenuItem, ArrowButtonsModeMenuItem, TabDropdownModeMenuItem);

            // The DataContext for CurrentTabOverflowMode binding might need to be set to _tabOverflowManager if XAML binds to it.
            // If MainWindow still needs to expose it, it should be a pass-through to _tabOverflowManager.CurrentTabOverflowMode.
            // For now, assuming direct calls to _tabOverflowManager.SetOverflowMode() from UI event handlers.
            // If XAML bindings like <DataTrigger Binding="{Binding CurrentTabOverflowMode}" ...> are used,
            // then MainWindow might need to expose CurrentTabOverflowMode and delegate to _tabOverflowManager,
            // or the DataContext of the TabControl or relevant parent needs to be set to _tabOverflowManager.
            // For simplicity of this refactoring step, we'll assume XAML event handlers call manager's methods.
            // The XAML currently has: <DataTrigger Binding="{Binding CurrentTabOverflowMode}" Value="Scrollbar">
            // This means `this.DataContext = this;` should be kept, and MainWindow needs to expose CurrentTabOverflowMode.

            // Re-instating DataContext and CurrentTabOverflowMode property that delegates to the manager.
            this.DataContext = this;
        }

        // Expose CurrentTabOverflowMode for XAML binding, delegating to the manager
        public TabOverflowMode CurrentTabOverflowMode
        {
            get
            {
                // Ensure _tabOverflowManager is initialized before accessing, provide a default if not.
                return _tabOverflowManager != null ? _tabOverflowManager.CurrentTabOverflowMode : default(TabOverflowMode);
            }
            set
            {
                // Check if manager exists and if the value is actually changing
                if (_tabOverflowManager != null && _tabOverflowManager.CurrentTabOverflowMode != value)
                {
                    _tabOverflowManager.SetOverflowMode(value); // Call the public method
                                                                    // The manager's setter should handle saving and internal UI updates.
                    OnPropertyChanged(); // Notify XAML that this MainWindow property changed
                }
                else if (_tabOverflowManager == null && value != default(TabOverflowMode))
                {
                    // This case might occur if XAML tries to set a value before MainTabControl_Loaded initializes _tabOverflowManager.
                    // Depending on desired behavior, could queue the value, log, or ignore.
                    // For now, this path does nothing if manager isn't ready.
                    // Consider if default(TabOverflowMode) (which is Scrollbar) is the right default if manager is null.
                    // The getter already defaults to Scrollbar if manager is null (as TabOverflowMode.Scrollbar is 0).
                    // If the XAML binding sets a different initial value before manager is ready, this could be an issue.
                    // However, the manager loads the persisted value on init, which should then propagate.
                }
            }
        }


        private void LoadNavigationOptionStates()
        {
            _settings = AppSettingsService.LoadAppSettings();
            // Ensure _settings is not null, though LoadAppSettings should return new AppSettings() if file is missing/corrupt
            if (_settings == null) _settings = new AppSettings();

            KeyboardArrowsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows);
            GridClickOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick);
            VisibleButtonsOption.IsChecked = _settings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);
        }

        private void NavigationOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null)
            {
                // This could happen if event fires before _settings is initialized, though unlikely with constructor setup.
                // Load settings as a fallback.
                _settings = AppSettingsService.LoadAppSettings();
                if (_settings == null) _settings = new AppSettings(); // Ensure not null
            }

            NavigationMethod currentMethods = NavigationMethod.None;
            if (KeyboardArrowsOption.IsChecked) currentMethods |= NavigationMethod.KeyboardArrows;
            if (GridClickOption.IsChecked) currentMethods |= NavigationMethod.GridClick;
            if (VisibleButtonsOption.IsChecked) currentMethods |= NavigationMethod.VisibleButtons;

            // "At least one" rule
            if (currentMethods == NavigationMethod.None)
            {
                if (sender is MenuItem menuItem)
                {
                    menuItem.IsChecked = true; // Re-check the item that was just unchecked
                }
                // Optional: MessageBox.Show("At least one page navigation method must be selected.", "Options Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Prevent saving NavigationMethod.None
            }

            _settings.EnabledNavigationMethods = currentMethods;
            AppSettingsService.SaveAppSettings(_settings);

            // If ImageTabControl instances need to be updated dynamically, an eventing mechanism would be needed here.
            // For now, new ImageTabControls will pick up the new settings upon creation.
        }

        // LeftScrollButton_Click MOVED to TabOverflowManager
        // RightScrollButton_Click MOVED to TabOverflowManager
        // UpdateScrollButtonVisibility MOVED to TabOverflowManager
        // TabItemsScrollViewer_ScrollChanged MOVED to TabOverflowManager
        // TabListDropdownButton_Click MOVED to TabOverflowManager
        // ContextMenuItem_Click MOVED to TabOverflowManager

        private void SetOverflowMode_Scrollbar_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManager?.SetOverflowMode(TabOverflowMode.Scrollbar);
            // UpdateMenuCheckedStates(); // TabOverflowManager handles this
        }

        private void SetOverflowMode_Arrows_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManager?.SetOverflowMode(TabOverflowMode.ArrowButtons);
            // UpdateMenuCheckedStates(); // TabOverflowManager handles this
        }

        private void SetOverflowMode_Dropdown_Click(object sender, RoutedEventArgs e)
        {
            _tabOverflowManager?.SetOverflowMode(TabOverflowMode.TabDropdown);
            // UpdateMenuCheckedStates(); // TabOverflowManager handles this
        }

        // UpdateMenuCheckedStates MOVED to TabOverflowManager

        private async Task OpenRandomChapter(bool switchToTab)
        {
            if (Views == null || !Views.Any())
            {
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

            List<string> imagePaths = null;
            try
            {
                imagePaths = await Task.Run(() => Directory.EnumerateFiles(chapterDirectoryInfo.FullName)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    .ToList());
            }
            catch (Exception ex) // Restore ex for MessageBox
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

        private void HandleChapterOpenRequested(object? sender, ChapterOpenRequestedEventArgs e) // Made sender nullable
        {
            AddImageTab(e.DirectoryPath, e.ImagePaths, e.SwitchToTab);
        }
    }
}

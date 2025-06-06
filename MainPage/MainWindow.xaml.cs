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

namespace Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";

        private ScrollViewer _tabItemsScrollViewer;
        private RepeatButton _leftScrollButton;
        private RepeatButton _rightScrollButton;
        private Button _tabListDropdownButton;

        private TabOverflowMode _currentTabOverflowMode = TabOverflowMode.Scrollbar; // Default mode
        public TabOverflowMode CurrentTabOverflowMode
        {
            get => _currentTabOverflowMode;
            set
            {
                if (_currentTabOverflowMode != value)
                {
                    _currentTabOverflowMode = value;
                    OnPropertyChanged(); // Notify XAML that the property has changed
                    SaveCurrentOverflowModeSetting(); // New method call to save
                }
            }
        }

        /// <summary>
        /// Gets the collection of ChapterListElement items to be displayed.
        /// This collection is bound to the ItemsControl in the XAML.
        /// </summary>
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this; // Ensure this is set

            LoadPersistedTabOverflowMode(); // New method call

            LoadChapterListAsync(); // Existing method
        }

        private void LoadPersistedTabOverflowMode()
        {
            AppSettings settings = AppSettingsService.LoadAppSettings();
            if (!string.IsNullOrEmpty(settings.DefaultTabOverflowMode))
            {
                if (Enum.TryParse<TabOverflowMode>(settings.DefaultTabOverflowMode, out TabOverflowMode mode))
                {
                    // Set the property directly to avoid re-saving immediately if it's the same as default
                    _currentTabOverflowMode = mode;
                    OnPropertyChanged(nameof(CurrentTabOverflowMode));
                }
                // else: log error about invalid mode string if desired
            }
            // If no persisted setting, it will use the default value set in the _currentTabOverflowMode field initializer.
            // UpdateMenuCheckedStates() is called in MainTabControl_Loaded, which will reflect this loaded mode.
        }

        private void SaveCurrentOverflowModeSetting()
        {
            AppSettings settings = AppSettingsService.LoadAppSettings(); // Load current or default settings
            settings.DefaultTabOverflowMode = CurrentTabOverflowMode.ToString(); // Update the mode
            AppSettingsService.SaveAppSettings(settings); // Save all settings
        }

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            ChapterListElement chapterListElement = new(directory)
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };

            chapterListElement.SetLabelText(directory.Name);
            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));

            Views.Add(chapterListElement);

            var imageSourceUri = await Task.Run(() => Tools.GetFirstImageInDirectory(directory));

            if (imageSourceUri != null)
            {
                BitmapImage? finalThumbnail = await Task.Run(() => {
                    var (width, height) = Tools.GetImageDimensions(imageSourceUri.LocalPath);
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
                List<DirectoryInfo> chapters = await Task.Run(() => Tools.GetDirectories(""));

                foreach (var directory in chapters)
                {
                    await ProcessChapterDirectoryAsync(directory);
                }
                MainTabHeaderTextBlock.Text += " (Loaded)";
            }
            catch (Exception ex)
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
                var tabItem = Tools.FindParent<TabItem>((DependencyObject)e.OriginalSource);
                if (tabItem != null && tabItem != MainTab)
                {
                    MainTabControl.Items.Remove(tabItem);
                }
            }
        }

        private void MainTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure the template is applied so we can find parts
            MainTabControl.ApplyTemplate();

            _tabItemsScrollViewer = MainTabControl.Template.FindName("TabItemsScrollViewer", MainTabControl) as ScrollViewer;
            _leftScrollButton = MainTabControl.Template.FindName("LeftScrollButton", MainTabControl) as RepeatButton;
            _rightScrollButton = MainTabControl.Template.FindName("RightScrollButton", MainTabControl) as RepeatButton;
            _tabListDropdownButton = MainTabControl.Template.FindName("TabListDropdownButton", MainTabControl) as Button;

            if (_leftScrollButton != null)
            {
                _leftScrollButton.Click += LeftScrollButton_Click;
            }
            if (_rightScrollButton != null)
            {
                _rightScrollButton.Click += RightScrollButton_Click;
            }
            if (_tabListDropdownButton != null)
            {
                _tabListDropdownButton.Click += TabListDropdownButton_Click;
                // Attempt to find the ContextMenu resource from MainWindow's resources
                var contextMenu = this.TryFindResource("TabListContextMenu") as ContextMenu;
                if (contextMenu != null)
                {
                    _tabListDropdownButton.ContextMenu = contextMenu;
                }
            }

            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.ScrollChanged += TabItemsScrollViewer_ScrollChanged;
            }

            UpdateScrollButtonVisibility(); // Call to set initial state of scroll buttons
            UpdateMenuCheckedStates(); // Call to set initial state of menu checks
        }

        private void LeftScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.LineLeft();
                UpdateScrollButtonVisibility();
            }
        }

        private void RightScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.LineRight();
                UpdateScrollButtonVisibility();
            }
        }

        private void UpdateScrollButtonVisibility()
        {
            if (_tabItemsScrollViewer == null || _leftScrollButton == null || _rightScrollButton == null || _tabListDropdownButton == null)
                return;

            // Arrow button logic (existing)
            _leftScrollButton.IsEnabled = _tabItemsScrollViewer.HorizontalOffset > 0;
            _rightScrollButton.IsEnabled = _tabItemsScrollViewer.HorizontalOffset < _tabItemsScrollViewer.ScrollableWidth;

            // The visibility of _tabListDropdownButton is now controlled by DataTriggers in XAML.
            // Optional: Could manage IsEnabled state here if desired, e.g., disable if !hasOverflow and CurrentTabOverflowMode is TabDropdown
        }

        private void TabItemsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // We only care about horizontal scroll changes for button visibility
            if (e.HorizontalChange != 0 || e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
            {
                UpdateScrollButtonVisibility();
            }
        }

        private void TabListDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabListDropdownButton == null || _tabListDropdownButton.ContextMenu == null)
                return;

            ContextMenu contextMenu = _tabListDropdownButton.ContextMenu;
            contextMenu.Items.Clear(); // Clear previous items

            foreach (object item in MainTabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    // Skip the "Add Tab Button Tab" if it exists and is not a real tab
                    if (tabItem.Name == "AddTabButtonTab" && tabItem.Header is Button) continue;


                    MenuItem menuItem = new MenuItem();
                    // Try to get header text, could be a string or a FrameworkElement like TextBlock
                    string headerText = (tabItem.Header is TextBlock tb) ? tb.Text : tabItem.Header?.ToString();

                    // Special handling for the main tab if its header is complex or not easily stringified
                    if (string.IsNullOrEmpty(headerText) && tabItem == MainTab && MainTabHeaderTextBlock != null)
                    {
                        headerText = MainTabHeaderTextBlock.Text;
                    }

                    menuItem.Header = headerText ?? "Unnamed Tab";
                    menuItem.Tag = tabItem; // Store the TabItem itself
                    menuItem.Click += ContextMenuItem_Click; // Handler for when a tab is selected from menu
                    contextMenu.Items.Add(menuItem);
                }
            }

            if (contextMenu.HasItems)
            {
                contextMenu.PlacementTarget = _tabListDropdownButton;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private void ContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabItem tabItem)
            {
                MainTabControl.SelectedItem = tabItem;

                // Ensure the tab item is visible within the scroll viewer
                if (_tabItemsScrollViewer != null && tabItem.IsVisible)
                {
                    // It's important that tabItem is part of the visual tree and has been rendered.
                    // Being selected should ensure it's loaded.
                    // We need to allow the layout to update after selection before bringing it into view.
                    // Dispatcher can help here.
                    tabItem.Dispatcher.BeginInvoke(new Action(() => {
                        tabItem.BringIntoView();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void SetOverflowMode_Scrollbar_Click(object sender, RoutedEventArgs e)
        {
            CurrentTabOverflowMode = TabOverflowMode.Scrollbar;
            UpdateMenuCheckedStates();
        }

        private void SetOverflowMode_Arrows_Click(object sender, RoutedEventArgs e)
        {
            CurrentTabOverflowMode = TabOverflowMode.ArrowButtons;
            UpdateMenuCheckedStates();
        }

        private void SetOverflowMode_Dropdown_Click(object sender, RoutedEventArgs e)
        {
            CurrentTabOverflowMode = TabOverflowMode.TabDropdown;
            UpdateMenuCheckedStates();
        }

        private void UpdateMenuCheckedStates()
        {
            // These MenuItems are defined with x:Name in MainWindow.xaml, so they are fields in this class.
            if (ScrollbarModeMenuItem != null)
                ScrollbarModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.Scrollbar);

            if (ArrowButtonsModeMenuItem != null)
                ArrowButtonsModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.ArrowButtons);

            if (TabDropdownModeMenuItem != null)
                TabDropdownModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.TabDropdown);
        }

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
    }
}

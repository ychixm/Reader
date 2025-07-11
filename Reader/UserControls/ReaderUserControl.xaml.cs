using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// Remove: using Reader.Business;
using Reader.Models; // For ReaderSettings, NavigationMethod etc.
using Utils.Controls;
using Utils.Models;   // For TabOverflowMode
using Utils; // For WpfHelpers, AppSettingsService
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
        private readonly ILoggerService _logger;
        private ReaderSettings _settings; // Changed type to ReaderSettings
        private TabOverflowManagementControl? _tabOverflowManagementCtrl;

        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };
        private const string PlaceholderImageRelativePath = "Ressources/NoImage.png";
        public ObservableCollection<ChapterListElement> Views { get; } = new ObservableCollection<ChapterListElement>();
        private const int MaxTitleLength = 40;
        private ChapterListElement? _randomChapterElement;

        public ReaderUserControl(ILoggerService logger)
        {
            InitializeComponent();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());
            _tabOverflowManagementCtrl = this.TabOverflowControl;
            _logger.LogInfo("ReaderUserControl initialized.");
            //LoadChapterListAsync(); // Called from Loaded event handler or explicitly after construction
        }

        private async Task ProcessChapterDirectoryAsync(DirectoryInfo directory)
        {
            ChapterListElement chapterListElement = new(directory, _logger) // Pass logger
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
            };
            chapterListElement.ChapterOpenRequested += HandleChapterOpenRequested;
            chapterListElement.SetLabelText(directory.Name);

            string placeholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlaceholderImageRelativePath);
            try
            {
                chapterListElement.SetImageSource(new BitmapImage(new Uri(placeholderPath, UriKind.Absolute)));
            }
            catch(Exception ex_placeholder)
            {
                _logger.LogError(ex_placeholder, "Failed to load placeholder image for chapter {ChapterName} from {PlaceholderPath}", directory.Name, placeholderPath);
            }

            Views.Add(chapterListElement);

            try
            {
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
            catch (Exception ex_process_chapter)
            {
                _logger.LogError(ex_process_chapter, "Error processing chapter directory {DirectoryName} for image loading", directory.Name);
            }
        }

        private async void LoadChapterListAsync()
        {
            _logger.LogInfo("LoadChapterListAsync started.");
            string effectivePath = string.Empty; // Initialize to ensure it's always assigned before use in catch
            try
            {
                string randomChapterPlaceholderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RandomChapterPlaceholder");
                Directory.CreateDirectory(randomChapterPlaceholderPath);
                DirectoryInfo randomChapterDirInfo = new DirectoryInfo(randomChapterPlaceholderPath);

                _randomChapterElement = new ChapterListElement(randomChapterDirInfo, _logger) // Pass logger
                {
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1),
                    IsSpecialRandomElement = true
                };
                _randomChapterElement.SetLabelText("Random Chapter");

                _randomChapterElement.MouseLeftButtonUp += async (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        await OpenRandomChapter(true);
                    }
                };
                _randomChapterElement.MouseDown += async (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        await OpenRandomChapter(false);
                    }
                };

                Views.Insert(0, _randomChapterElement);

                if (_settings != null && !string.IsNullOrEmpty(_settings.DefaultPath))
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
                _logger.LogError(ex, "Error loading chapter list. Effective path was {EffectivePath}", effectivePath);
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
                _logger.LogInfo("Existing tab found for {DirectoryPath}. Switching to it.", directoryPath);
                return;
            }
            _logger.LogInfo("Creating new image tab for {DirectoryPath}.", directoryPath);
            var imageTabControl = new ImageTabControl(imagePaths, _logger); // Pass logger
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
                Utils.Models.TabOverflowMode initialMode = Utils.Models.TabOverflowMode.Scrollbar;
                if (_settings != null && !string.IsNullOrEmpty(_settings.DefaultTabOverflowMode))
                {
                    Enum.TryParse<Utils.Models.TabOverflowMode>(_settings.DefaultTabOverflowMode, out initialMode);
                }

                _tabOverflowManagementCtrl.InitializeManager(
                    MainTabControl,
                    MainTabHeaderTextBlock,
                    initialMode
                );
                _tabOverflowManagementCtrl.ModeChanged += TabOverflowManagementCtrl_ModeChanged;
            }
            this.DataContext = this;
        }

        private void TabOverflowManagementCtrl_ModeChanged(Utils.Models.TabOverflowMode newMode)
        {
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
                }
            }
        }

        public void ApplyNavigationSettings(NavigationMethod newMethods)
        {
            if (_settings != null)
            {
                _settings.EnabledNavigationMethods = newMethods;
                // Note: Settings are not saved here; ReaderSubApplication.ApplyOptions does that via ReaderOptionsViewModel.
            }

            if (MainTabControl != null)
            {
                foreach (var item in MainTabControl.Items)
                {
                    if (item is TabItem tabItem && tabItem.Content is ImageTabControl imageTabCtrl)
                    {
                        // This reflection based approach is a bit fragile.
                        // Consider an interface or direct method calls if ImageTabControl properties are stable.
                        var enableGridClickProp = imageTabCtrl.GetType().GetProperty("EnableGridClick");
                        if (enableGridClickProp != null && enableGridClickProp.CanWrite)
                        {
                            enableGridClickProp.SetValue(imageTabCtrl, newMethods.HasFlag(NavigationMethod.GridClick));
                        }

                        var showNavButtonsProp = imageTabCtrl.GetType().GetProperty("ShowNavigationButtons");
                        if (showNavButtonsProp != null && showNavButtonsProp.CanWrite)
                        {
                            showNavButtonsProp.SetValue(imageTabCtrl, newMethods.HasFlag(NavigationMethod.VisibleButtons));
                        }
                    }
                }
            }
        }

        public void ApplyTabOverflowMode(Utils.Models.TabOverflowMode newMode)
        {
            if (_tabOverflowManagementCtrl != null)
            {
                _tabOverflowManagementCtrl.SetOverflowMode(newMode);
            }

            if (_settings != null)
            {
                _settings.DefaultTabOverflowMode = newMode.ToString();
                // Settings are not saved here; ReaderSubApplication.ApplyOptions does that.
            }
            OnPropertyChanged(nameof(CurrentTabOverflowMode));
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
                _logger.LogError(ex, "Error reading images for random chapter from directory {DirectoryFullName}", chapterDirectoryInfo.FullName);
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

using ReaderUtils.Models; // Changed from Reader.Models
using ReaderUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
// Assuming AppSettings and AppSettingsService will be handled or moved later.
// For now, this will cause errors if AppSettingsService is not accessible.
// To make it potentially compile for now, I might need to comment out AppSettingsService lines.
// AppSettings related functionality will be temporarily commented out.

namespace ReaderUtils.Business // Changed from Reader.Business
{
    public class TabOverflowManager
    {
        private readonly TabControl _tabControl;
        private readonly Window? _ownerWindow;

        private ScrollViewer? _tabItemsScrollViewer;
        private RepeatButton? _leftScrollButton;
        private RepeatButton? _rightScrollButton;
        private Button? _tabListDropdownButton;

        private readonly MenuItem? _scrollbarModeMenuItem;
        private readonly MenuItem? _arrowButtonsModeMenuItem;
        private readonly MenuItem? _tabDropdownModeMenuItem;

        private TabOverflowMode _currentTabOverflowMode = TabOverflowMode.Scrollbar;
        public TabOverflowMode CurrentTabOverflowMode
        {
            get => _currentTabOverflowMode;
            private set
            {
                if (_currentTabOverflowMode != value)
                {
                    _currentTabOverflowMode = value;
                    SaveCurrentOverflowModeSetting();
                    UpdateMenuCheckedStates();
                    UpdateUiForOverflowMode();
                }
            }
        }

        public TabOverflowManager(TabControl tabControl, Window ownerWindow, MenuItem? scrollbarModeMenuItem, MenuItem? arrowButtonsModeMenuItem, MenuItem? tabDropdownModeMenuItem)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _ownerWindow = ownerWindow;

            _scrollbarModeMenuItem = scrollbarModeMenuItem;
            _arrowButtonsModeMenuItem = arrowButtonsModeMenuItem;
            _tabDropdownModeMenuItem = tabDropdownModeMenuItem;

            InitializeCommon();
        }

        public TabOverflowManager(TabControl tabControl, Window? ownerWindow)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _ownerWindow = ownerWindow;

            _scrollbarModeMenuItem = null;
            _arrowButtonsModeMenuItem = null;
            _tabDropdownModeMenuItem = null;

            InitializeCommon();
        }

        private void InitializeCommon()
        {
            _tabControl.ApplyTemplate();

            _tabItemsScrollViewer = _tabControl.Template.FindName("PART_ScrollViewer", _tabControl) as ScrollViewer;
            if (_tabItemsScrollViewer == null)
            {
                _tabItemsScrollViewer = _tabControl.Template.FindName("TabItemsScrollViewer", _tabControl) as ScrollViewer;
            }
            if (_tabItemsScrollViewer == null && _tabControl.HasItems && _tabControl.Items.Count > 0)
            {
                if (VisualTreeHelper.GetChildrenCount(_tabControl) > 0)
                   _tabItemsScrollViewer = WpfHelpers.FindVisualChild<ScrollViewer>(_tabControl);
            }

            if (_tabItemsScrollViewer == null)
            {
                Debug.WriteLine("WARN: TabItemsScrollViewer (or PART_ScrollViewer) not found in TabControl template. Scroll-dependent overflow modes will be affected.");
            }
            else
            {
                _tabItemsScrollViewer.ScrollChanged += TabItemsScrollViewer_ScrollChanged;
            }

            _leftScrollButton = _tabControl.Template.FindName("LeftScrollButton", _tabControl) as RepeatButton;
            _rightScrollButton = _tabControl.Template.FindName("RightScrollButton", _tabControl) as RepeatButton;
            _tabListDropdownButton = _tabControl.Template.FindName("TabListDropdownButton", _tabControl) as Button;

            if (_leftScrollButton != null) _leftScrollButton.Click += LeftScrollButton_Click;
            else Debug.WriteLine("WARN: LeftScrollButton not found in TabControl template.");

            if (_rightScrollButton != null) _rightScrollButton.Click += RightScrollButton_Click;
            else Debug.WriteLine("WARN: RightScrollButton not found in TabControl template.");

            if (_tabListDropdownButton != null)
            {
                _tabListDropdownButton.Click += TabListDropdownButton_Click;
                if (_ownerWindow != null && _ownerWindow.TryFindResource("TabListContextMenu") is ContextMenu contextMenu)
                {
                    _tabListDropdownButton.ContextMenu = contextMenu;
                }
                else if (_ownerWindow == null)
                {
                     Debug.WriteLine("INFO: Owner window is null, cannot attach TabListContextMenu by resource name for TabListDropdownButton.");
                }
                else
                {
                    Debug.WriteLine("WARN: TabListContextMenu not found as a resource in the owner window for TabListDropdownButton.");
                }
            }
            else
            {
                Debug.WriteLine("WARN: TabListDropdownButton not found in TabControl template.");
            }

            LoadPersistedTabOverflowMode();
            UpdateUiForOverflowMode();
        }

        public TabOverflowMode LoadPersistedTabOverflowMode()
        {
            // AppSettingsService will cause an error here as it's in Reader project.
            // This needs to be refactored or AppSettings moved.
            // Temporarily defaulting to Scrollbar and skipping load/save.
            // AppSettings settings = AppSettingsService.LoadAppSettings();
            // if (!string.IsNullOrEmpty(settings.DefaultTabOverflowMode))
            // {
            //     if (Enum.TryParse<TabOverflowMode>(settings.DefaultTabOverflowMode, out TabOverflowMode mode))
            //     {
            //         CurrentTabOverflowMode = mode;
            //         return mode;
            //     }
            // }
            CurrentTabOverflowMode = TabOverflowMode.Scrollbar;
            return CurrentTabOverflowMode;
        }

        private void SaveCurrentOverflowModeSetting()
        {
            // AppSettingsService will cause an error here.
            // Temporarily skipping save.
            // AppSettings settings = AppSettingsService.LoadAppSettings();
            // settings.DefaultTabOverflowMode = CurrentTabOverflowMode.ToString();
            // AppSettingsService.SaveAppSettings(settings);
            Debug.WriteLine("INFO: SaveCurrentOverflowModeSetting skipped in ReaderUtils.TabOverflowManager due to AppSettingsService dependency.");
        }

        public void SetOverflowMode(TabOverflowMode mode, bool updateUiElements = true)
        {
            CurrentTabOverflowMode = mode;
            if (updateUiElements) UpdateUiForOverflowMode();
        }

        private void UpdateUiForOverflowMode()
        {
            bool isScrollbarMode = CurrentTabOverflowMode == TabOverflowMode.Scrollbar;
            bool isArrowButtonsMode = CurrentTabOverflowMode == TabOverflowMode.ArrowButtons;

            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.HorizontalScrollBarVisibility = isScrollbarMode ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
            }

            if (_leftScrollButton != null) _leftScrollButton.Visibility = isArrowButtonsMode ? Visibility.Visible : Visibility.Collapsed;
            if (_rightScrollButton != null) _rightScrollButton.Visibility = isArrowButtonsMode ? Visibility.Visible : Visibility.Collapsed;

            UpdateScrollButtonEnablement();
        }

        private void UpdateScrollButtonEnablement()
        {
            if (CurrentTabOverflowMode == TabOverflowMode.ArrowButtons)
            {
                if (_leftScrollButton != null) _leftScrollButton.IsEnabled = _tabItemsScrollViewer != null && _tabItemsScrollViewer.HorizontalOffset > 0;
                if (_rightScrollButton != null) _rightScrollButton.IsEnabled = _tabItemsScrollViewer != null && _tabItemsScrollViewer.HorizontalOffset < _tabItemsScrollViewer.ScrollableWidth;
            }
            else
            {
                if (_leftScrollButton != null) _leftScrollButton.IsEnabled = false;
                if (_rightScrollButton != null) _rightScrollButton.IsEnabled = false;
            }
        }


        private void TabItemsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (CurrentTabOverflowMode == TabOverflowMode.ArrowButtons && (e.HorizontalChange != 0 || e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0))
            {
                UpdateScrollButtonEnablement();
            }
        }

        private void LeftScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null && CurrentTabOverflowMode == TabOverflowMode.ArrowButtons)
            {
                _tabItemsScrollViewer.LineLeft();
            }
        }

        private void RightScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null && CurrentTabOverflowMode == TabOverflowMode.ArrowButtons)
            {
                _tabItemsScrollViewer.LineRight();
            }
        }

        private void TabListDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabListDropdownButton == null || _tabListDropdownButton.ContextMenu == null)
            {
                Debug.WriteLine("TabListDropdownButton or its ContextMenu is null. Cannot show tab list.");
                return;
            }

            ContextMenu contextMenu = _tabListDropdownButton.ContextMenu;
            contextMenu.Items.Clear();

            TextBlock? mainTabHeaderTextBlock = null;
            if (_ownerWindow != null)
            {
                mainTabHeaderTextBlock = _ownerWindow.FindName("MainTabHeaderTextBlock") as TextBlock;
            }

            foreach (object item in _tabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    if (tabItem.Name == "AddTabButtonTab" && tabItem.Header is Button) continue;

                    MenuItem menuItem = new MenuItem();
                    string? headerText = (tabItem.Header is TextBlock tb) ? tb.Text : tabItem.Header?.ToString();

                    if (string.IsNullOrEmpty(headerText) && tabItem == _tabControl.Items.OfType<TabItem>().FirstOrDefault() && mainTabHeaderTextBlock != null)
                    {
                         headerText = mainTabHeaderTextBlock.Text;
                    }

                    menuItem.Header = headerText ?? "Unnamed Tab";
                    menuItem.Tag = tabItem;
                    menuItem.Click += ContextMenuItem_Click;
                    contextMenu.Items.Add(menuItem);
                }
            }

            if (contextMenu.HasItems)
            {
                contextMenu.PlacementTarget = _tabListDropdownButton;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private void ContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabItem tabItem)
            {
                _tabControl.SelectedItem = tabItem;

                if (_tabItemsScrollViewer != null && tabItem.IsVisible)
                {
                    tabItem.Dispatcher.BeginInvoke(new Action(() => {
                        tabItem.BringIntoView();
                    }), DispatcherPriority.Background);
                }
            }
        }

        public void UpdateMenuCheckedStates()
        {
            if (_scrollbarModeMenuItem != null)
                _scrollbarModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.Scrollbar);

            if (_arrowButtonsModeMenuItem != null)
                _arrowButtonsModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.ArrowButtons);

            if (_tabDropdownModeMenuItem != null)
                _tabDropdownModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.TabDropdown);
        }
    }
}

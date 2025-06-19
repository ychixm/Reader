using Reader.Models;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Utils;
using Utils.Models;
// using System.Windows.Media; // Not strictly needed for this file's content

namespace Reader.Business
{
    public class TabOverflowManager
    {
        private readonly TabControl _tabControl;
        private readonly ScrollViewer _tabItemsScrollViewer;
        private readonly RepeatButton _leftScrollButton;
        private readonly RepeatButton _rightScrollButton;
        private readonly Button _tabListDropdownButton;
        private readonly ContextMenu _tabListContextMenu;
        private readonly TextBlock _mainTabHeaderTextBlock;

        private readonly MenuItem _scrollbarModeMenuItem;
        private readonly MenuItem _arrowButtonsModeMenuItem;
        private readonly MenuItem _tabDropdownModeMenuItem;

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
                    UpdateScrollButtonVisibility();
                }
            }
        }

        public TabOverflowManager(TabControl tabControl,
                                  ContextMenu tabListContextMenu,
                                  TextBlock mainTabHeaderTextBlock,
                                  MenuItem scrollbarModeMenuItem,
                                  MenuItem arrowButtonsModeMenuItem,
                                  MenuItem tabDropdownModeMenuItem)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _tabListContextMenu = tabListContextMenu ?? throw new ArgumentNullException(nameof(tabListContextMenu));
            _mainTabHeaderTextBlock = mainTabHeaderTextBlock; // Assuming it can be null based on original FindName behavior, or add null check if it must exist. For now, keeping as is.
            _scrollbarModeMenuItem = scrollbarModeMenuItem ?? throw new ArgumentNullException(nameof(scrollbarModeMenuItem));
            _arrowButtonsModeMenuItem = arrowButtonsModeMenuItem ?? throw new ArgumentNullException(nameof(arrowButtonsModeMenuItem));
            _tabDropdownModeMenuItem = tabDropdownModeMenuItem ?? throw new ArgumentNullException(nameof(tabDropdownModeMenuItem));

            _tabControl.ApplyTemplate();

            _tabItemsScrollViewer = _tabControl.Template.FindName("TabItemsScrollViewer", _tabControl) as ScrollViewer ?? throw new InvalidOperationException("TabItemsScrollViewer not found in TabControl template.");
            _leftScrollButton = _tabControl.Template.FindName("LeftScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("LeftScrollButton not found in TabControl template.");
            _rightScrollButton = _tabControl.Template.FindName("RightScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("RightScrollButton not found in TabControl template.");
            _tabListDropdownButton = _tabControl.Template.FindName("TabListDropdownButton", _tabControl) as Button ?? throw new InvalidOperationException("TabListDropdownButton not found in TabControl template.");

            _tabListDropdownButton.ContextMenu = _tabListContextMenu;

            _leftScrollButton.Click += LeftScrollButton_Click;
            _rightScrollButton.Click += RightScrollButton_Click;
            _tabListDropdownButton.Click += TabListDropdownButton_Click;
            _tabItemsScrollViewer.ScrollChanged += TabItemsScrollViewer_ScrollChanged;

            LoadPersistedTabOverflowMode();
            UpdateScrollButtonVisibility();
            UpdateMenuCheckedStates();
        }

        private void LoadPersistedTabOverflowMode()
        {
            AppSettings settings = AppSettingsService.LoadAppSettings();
            if (!string.IsNullOrEmpty(settings.DefaultTabOverflowMode))
            {
                if (Enum.TryParse<TabOverflowMode>(settings.DefaultTabOverflowMode, out TabOverflowMode mode))
                {
                    CurrentTabOverflowMode = mode;
                }
            }
        }

        private void SaveCurrentOverflowModeSetting()
        {
            AppSettings settings = AppSettingsService.LoadAppSettings();
            settings.DefaultTabOverflowMode = CurrentTabOverflowMode.ToString();
            AppSettingsService.SaveAppSettings(settings);
        }

        public void SetOverflowMode(TabOverflowMode mode)
        {
            CurrentTabOverflowMode = mode;
        }

        private void UpdateScrollButtonVisibility()
        {
            if (_tabItemsScrollViewer == null || _leftScrollButton == null || _rightScrollButton == null || _tabListDropdownButton == null)
                return;

            _leftScrollButton.IsEnabled = _tabItemsScrollViewer.HorizontalOffset > 0;
            _rightScrollButton.IsEnabled = _tabItemsScrollViewer.HorizontalOffset < _tabItemsScrollViewer.ScrollableWidth;
        }

        private void TabItemsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0 || e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
            {
                UpdateScrollButtonVisibility();
            }
        }

        private void LeftScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.LineLeft();
            }
        }

        private void RightScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.LineRight();
            }
        }

        private void TabListDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabListDropdownButton == null || _tabListDropdownButton.ContextMenu == null)
                return;

            ContextMenu contextMenu = _tabListDropdownButton.ContextMenu;
            contextMenu.Items.Clear();

            TextBlock? currentMainTabHeaderTextBlock = _mainTabHeaderTextBlock; // Use the field passed from constructor

            foreach (object item in _tabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    if (tabItem.Name == "AddTabButtonTab" && tabItem.Header is Button) continue;

                    MenuItem menuItem = new MenuItem();
                    string? headerText = (tabItem.Header is TextBlock tb) ? tb.Text : tabItem.Header?.ToString();

                    if (string.IsNullOrEmpty(headerText) &&
                        tabItem == _tabControl.Items.OfType<TabItem>().FirstOrDefault() &&
                        currentMainTabHeaderTextBlock != null) // Check _mainTabHeaderTextBlock before use
                    {
                         headerText = currentMainTabHeaderTextBlock.Text;
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
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
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
                    // It's important that BringIntoView is called after the tab is selected and visible.
                    // Dispatcher ensures it runs after layout updates.
                    tabItem.Dispatcher.BeginInvoke(new Action(() => {
                        tabItem.BringIntoView();
                    }), System.Windows.Threading.DispatcherPriority.Background);
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

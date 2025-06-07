using Reader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For RepeatButton, PlacementMode
using System.Windows.Input; // For MouseButtonEventArgs (if needed, ContextMenuItem_Click uses RoutedEventArgs)
using System.Windows.Media; // For VisualTreeHelper (if needed, though template finding is primary)
using System.Windows.Threading; // For DispatcherPriority

namespace Reader.Business
{
    public class TabOverflowManager
    {
        private readonly TabControl _tabControl;
        private readonly Window _ownerWindow;

        private readonly ScrollViewer _tabItemsScrollViewer;
        private readonly RepeatButton _leftScrollButton;
        private readonly RepeatButton _rightScrollButton;
        private readonly Button _tabListDropdownButton;

        // Menu items for updating checked states
        private readonly MenuItem _scrollbarModeMenuItem;
        private readonly MenuItem _arrowButtonsModeMenuItem;
        private readonly MenuItem _tabDropdownModeMenuItem;

        private TabOverflowMode _currentTabOverflowMode = TabOverflowMode.Scrollbar; // Default mode
        public TabOverflowMode CurrentTabOverflowMode
        {
            get => _currentTabOverflowMode;
            private set // Make setter private, controlled by SetOverflowMode method
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

        public TabOverflowManager(TabControl tabControl, Window ownerWindow, MenuItem scrollbarModeMenuItem, MenuItem arrowButtonsModeMenuItem, MenuItem tabDropdownModeMenuItem)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));

            _scrollbarModeMenuItem = scrollbarModeMenuItem ?? throw new ArgumentNullException(nameof(scrollbarModeMenuItem));
            _arrowButtonsModeMenuItem = arrowButtonsModeMenuItem ?? throw new ArgumentNullException(nameof(arrowButtonsModeMenuItem));
            _tabDropdownModeMenuItem = tabDropdownModeMenuItem ?? throw new ArgumentNullException(nameof(tabDropdownModeMenuItem));

            _tabControl.ApplyTemplate();

            _tabItemsScrollViewer = _tabControl.Template.FindName("TabItemsScrollViewer", _tabControl) as ScrollViewer ?? throw new InvalidOperationException("TabItemsScrollViewer not found in TabControl template.");
            _leftScrollButton = _tabControl.Template.FindName("LeftScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("LeftScrollButton not found in TabControl template.");
            _rightScrollButton = _tabControl.Template.FindName("RightScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("RightScrollButton not found in TabControl template.");
            _tabListDropdownButton = _tabControl.Template.FindName("TabListDropdownButton", _tabControl) as Button ?? throw new InvalidOperationException("TabListDropdownButton not found in TabControl template.");

            _leftScrollButton.Click += LeftScrollButton_Click;
            _rightScrollButton.Click += RightScrollButton_Click;
            _tabListDropdownButton.Click += TabListDropdownButton_Click;
            _tabItemsScrollViewer.ScrollChanged += TabItemsScrollViewer_ScrollChanged;

            var contextMenu = _ownerWindow.TryFindResource("TabListContextMenu") as ContextMenu;
            if (contextMenu != null)
            {
                _tabListDropdownButton.ContextMenu = contextMenu;
            }

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
                    // Set the backing field directly to avoid re-saving/re-updating UI if it's the same as default
                    _currentTabOverflowMode = mode;
                }
                // else: log error about invalid mode string if desired
            }
            // If no persisted setting, it will use the default value set in the _currentTabOverflowMode field initializer.
        }

        private void SaveCurrentOverflowModeSetting()
        {
            AppSettings settings = AppSettingsService.LoadAppSettings();
            settings.DefaultTabOverflowMode = CurrentTabOverflowMode.ToString();
            AppSettingsService.SaveAppSettings(settings);
        }

        public void SetOverflowMode(TabOverflowMode mode)
        {
            CurrentTabOverflowMode = mode; // This will trigger the setter logic including saving and UI updates
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
                // UpdateScrollButtonVisibility(); // Already called by ScrollChanged
            }
        }

        private void RightScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.LineRight();
                // UpdateScrollButtonVisibility(); // Already called by ScrollChanged
            }
        }

        private void TabListDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tabListDropdownButton == null || _tabListDropdownButton.ContextMenu == null)
                return;

            ContextMenu contextMenu = _tabListDropdownButton.ContextMenu;
            contextMenu.Items.Clear();

            // Need access to MainTab and MainTabHeaderTextBlock from MainWindow.
            // This is a bit tricky. For now, let's assume MainTab is identifiable by Name or Type.
            // And MainTabHeaderTextBlock is also named.
            // A cleaner way would be for MainWindow to pass these or a delegate to get them.
            // For now, let's try finding MainTabHeaderTextBlock from _ownerWindow if possible, or make assumptions.
            TextBlock mainTabHeaderTextBlock = _ownerWindow.FindName("MainTabHeaderTextBlock") as TextBlock;


            foreach (object item in _tabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    if (tabItem.Name == "AddTabButtonTab" && tabItem.Header is Button) continue;

                    MenuItem menuItem = new MenuItem();
                    string headerText = (tabItem.Header is TextBlock tb) ? tb.Text : tabItem.Header?.ToString();

                    // Attempt to get the header text for the main tab specifically
                    // This relies on MainTab having a specific name or being the first tab.
                    // The original code used `tabItem == MainTab` which is not possible here directly.
                    // If the main tab's header is complex, this might need adjustment or a delegate.
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

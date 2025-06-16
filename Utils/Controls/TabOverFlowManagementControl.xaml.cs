using Utils.Models;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Utils.Controls
{
    public partial class TabOverflowManagementControl : UserControl
    {
        public event Action<TabOverflowMode>? ModeChanged;

        private TabControl? _tabControl;
        private ScrollViewer? _tabItemsScrollViewer;
        private RepeatButton? _leftScrollButton;
        private RepeatButton? _rightScrollButton;
        private Button? _tabListDropdownButton;
        private ContextMenu? _tabListContextMenu;
        private TextBlock? _mainTabHeaderTextBlock;

        // private MenuItem? _scrollbarModeMenuItem; // No longer managed here
        // private MenuItem? _arrowButtonsModeMenuItem; // No longer managed here
        // private MenuItem? _tabDropdownModeMenuItem; // No longer managed here

        private TabOverflowMode _currentTabOverflowMode = TabOverflowMode.Scrollbar;
        public TabOverflowMode CurrentTabOverflowMode
        {
            get => _currentTabOverflowMode;
            private set
            {
                if (_currentTabOverflowMode != value)
                {
                    _currentTabOverflowMode = value;
                    ModeChanged?.Invoke(_currentTabOverflowMode);
                    // UpdateMenuCheckedStates(); // External menu items no longer managed here
                    UpdateScrollButtonVisibility();
                }
            }
        }

        public TabOverflowManagementControl()
        {
            InitializeComponent();
            _tabListContextMenu = this.FindName("TabListContextMenu") as ContextMenu;
            if (_tabListContextMenu == null)
            {
                // Fallback or ensure XAML guarantees its presence if critical before InitializeManager
                _tabListContextMenu = new ContextMenu();
            }
        }

        // Modified InitializeManager to remove direct dependency on external MenuItems
        public void InitializeManager(TabControl tabControl, TextBlock mainTabHeaderTextBlock, TabOverflowMode initialMode)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _mainTabHeaderTextBlock = mainTabHeaderTextBlock;

            // _scrollbarModeMenuItem = scrollbarModeMenuItem ?? throw new ArgumentNullException(nameof(scrollbarModeMenuItem)); // Removed
            // _arrowButtonsModeMenuItem = arrowButtonsModeMenuItem ?? throw new ArgumentNullException(nameof(arrowButtonsModeMenuItem)); // Removed
            // _tabDropdownModeMenuItem = tabDropdownModeMenuItem ?? throw new ArgumentNullException(nameof(tabDropdownModeMenuItem)); // Removed

            _currentTabOverflowMode = initialMode;

            // _scrollbarModeMenuItem.Click += (s, e) => SetOverflowMode(TabOverflowMode.Scrollbar); // Removed
            // _arrowButtonsModeMenuItem.Click += (s, e) => SetOverflowMode(TabOverflowMode.ArrowButtons); // Removed
            // _tabDropdownModeMenuItem.Click += (s, e) => SetOverflowMode(TabOverflowMode.TabDropdown); // Removed

            _tabControl.ApplyTemplate();

            _tabItemsScrollViewer = _tabControl.Template.FindName("TabItemsScrollViewer", _tabControl) as ScrollViewer ?? throw new InvalidOperationException("TabItemsScrollViewer not found in TabControl template.");
            _leftScrollButton = _tabControl.Template.FindName("LeftScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("LeftScrollButton not found in TabControl template.");
            _rightScrollButton = _tabControl.Template.FindName("RightScrollButton", _tabControl) as RepeatButton ?? throw new InvalidOperationException("RightScrollButton not found in TabControl template.");
            _tabListDropdownButton = _tabControl.Template.FindName("TabListDropdownButton", _tabControl) as Button ?? throw new InvalidOperationException("TabListDropdownButton not found in TabControl template.");

            if (_tabListDropdownButton != null && _tabListContextMenu != null)
            {
                _tabListDropdownButton.ContextMenu = _tabListContextMenu;
            }

            if (_leftScrollButton != null) _leftScrollButton.Click += LeftScrollButton_Click;
            if (_rightScrollButton != null) _rightScrollButton.Click += RightScrollButton_Click;
            if (_tabListDropdownButton != null) _tabListDropdownButton.Click += TabListDropdownButton_Click;
            if (_tabItemsScrollViewer != null) _tabItemsScrollViewer.ScrollChanged += TabItemsScrollViewer_ScrollChanged;

            // CurrentTabOverflowMode is already set, now update UI based on it.
            UpdateScrollButtonVisibility();
            // UpdateMenuCheckedStates(); // External menu items no longer managed here
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

            bool showArrows = CurrentTabOverflowMode == TabOverflowMode.ArrowButtons;
            _leftScrollButton.Visibility = showArrows ? Visibility.Visible : Visibility.Collapsed;
            _rightScrollButton.Visibility = showArrows ? Visibility.Visible : Visibility.Collapsed;

            _tabListDropdownButton.Visibility = CurrentTabOverflowMode == TabOverflowMode.TabDropdown ? Visibility.Visible : Visibility.Collapsed;

            if (_tabItemsScrollViewer != null)
            {
                _tabItemsScrollViewer.HorizontalScrollBarVisibility = CurrentTabOverflowMode == TabOverflowMode.Scrollbar ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
            }
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
            if (_tabListDropdownButton == null || _tabListContextMenu == null || _tabControl == null)
                return;

            _tabListContextMenu.Items.Clear();

            TextBlock? currentMainTabHeaderTextBlock = _mainTabHeaderTextBlock;

            foreach (object item in _tabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    if (tabItem.Name == "AddTabButtonTab" && tabItem.Header is Button) continue;

                    MenuItem menuItem = new MenuItem();
                    string? headerText = (tabItem.Header is TextBlock tb) ? tb.Text : tabItem.Header?.ToString();

                    if (string.IsNullOrEmpty(headerText) &&
                        tabItem == _tabControl.Items.OfType<TabItem>().FirstOrDefault() &&
                        currentMainTabHeaderTextBlock != null)
                    {
                        headerText = currentMainTabHeaderTextBlock.Text;
                    }

                    menuItem.Header = headerText ?? "Unnamed Tab";
                    menuItem.Tag = tabItem;
                    menuItem.Click += ContextMenuItem_Click;
                    _tabListContextMenu.Items.Add(menuItem);
                }
            }

            if (_tabListContextMenu.HasItems)
            {
                _tabListContextMenu.PlacementTarget = _tabListDropdownButton;
                _tabListContextMenu.Placement = PlacementMode.Bottom;
                _tabListContextMenu.IsOpen = true;
            }
        }

        private void ContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabItem tabItem)
            {
                if (_tabControl != null) _tabControl.SelectedItem = tabItem;

                if (_tabItemsScrollViewer != null && tabItem.IsVisible)
                {
                    tabItem.Dispatcher.BeginInvoke(new Action(() => {
                        tabItem.BringIntoView();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        // This method is no longer needed as external menu items are not managed here.
        // public void UpdateMenuCheckedStates()
        // {
        //     if (_scrollbarModeMenuItem != null)
        //         _scrollbarModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.Scrollbar);
        //
        //     if (_arrowButtonsModeMenuItem != null)
        //         _arrowButtonsModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.ArrowButtons);
        //
        //     if (_tabDropdownModeMenuItem != null)
        //         _tabDropdownModeMenuItem.IsChecked = (CurrentTabOverflowMode == TabOverflowMode.TabDropdown);
        // }
    }
}

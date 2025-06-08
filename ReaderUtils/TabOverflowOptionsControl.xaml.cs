using System;
using System.Windows;
using System.Windows.Controls;
using ReaderUtils.Business; // Changed to ReaderUtils.Business
using ReaderUtils.Models;   // Changed to ReaderUtils.Models

namespace ReaderUtils
{
    public partial class TabOverflowOptionsControl : UserControl, IDisposable
    {
        private TabOverflowManager? _tabOverflowManager;
        private bool _isDisposed;

        public static readonly DependencyProperty TargetTabControlProperty =
            DependencyProperty.Register(
                nameof(TargetTabControl),
                typeof(TabControl),
                typeof(TabOverflowOptionsControl),
                new PropertyMetadata(null, OnTargetTabControlChanged));

        public TabControl TargetTabControl
        {
            get { return (TabControl)GetValue(TargetTabControlProperty); }
            set { SetValue(TargetTabControlProperty, value); }
        }

        public TabOverflowOptionsControl()
        {
            InitializeComponent();
        }

        private static void OnTargetTabControlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TabOverflowOptionsControl control)
            {
                control.InitializeManagerForNewTabControl(e.NewValue as TabControl);
            }
        }

        private void InitializeManagerForNewTabControl(TabControl? newTabControl)
        {
            // Dispose existing manager if any
            DisposeManager();

            if (newTabControl != null)
            {
                // Try to find the parent window for the TabOverflowManager context
                Window? parentWindow = WpfHelpers.FindParent<Window>(newTabControl);
                _tabOverflowManager = new TabOverflowManager(newTabControl, parentWindow);

                TabOverflowMode persistedMode = _tabOverflowManager.LoadPersistedTabOverflowMode();
                OverflowModeComboBox.SelectedIndex = (int)persistedMode;
                // Apply the mode via the manager to ensure UI consistency
                _tabOverflowManager.SetOverflowMode(persistedMode, updateUiElements: true);
                OverflowModeComboBox.IsEnabled = true;
            }
            else
            {
                OverflowModeComboBox.IsEnabled = false;
            }
        }

        private void OverflowModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tabOverflowManager != null && OverflowModeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Content is string contentString && Enum.TryParse<TabOverflowMode>(contentString, out var mode))
                {
                    if (_tabOverflowManager.CurrentTabOverflowMode != mode) // Check current mode from manager
                    {
                        _tabOverflowManager.SetOverflowMode(mode, updateUiElements: true);
                    }
                }
            }
        }

        private void DisposeManager()
        {
            if (_tabOverflowManager != null)
            {
                // If TabOverflowManager implements IDisposable, call it:
                // (_tabOverflowManager as IDisposable)?.Dispose();
                // For now, just nullify to release reference and stop further interaction
                _tabOverflowManager = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                // Dispose managed state (managed objects).
                DisposeManager();
            }
            _isDisposed = true;
        }
    }
}

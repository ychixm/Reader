using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls; // Required for TabControl, TabItem
using System.Windows.Input;   // Required for MouseButtonEventArgs
using System.Windows.Media;

namespace ReaderUtils
{
    public static class WpfHelpers
    {
        /// <summary>
        /// Finds the first parent of a given DependencyObject that is of the specified type.
        /// </summary>
        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }

        /// <summary>
        /// Finds the first visual child of a given DependencyObject that is of the specified type.
        /// </summary>
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T correctlyTypedChild)
                {
                    return correctlyTypedChild;
                }
                T? descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        /// <summary>
        /// Handles middle mouse button clicks on TabItems to close them, with exceptions.
        /// </summary>
        /// <param name="tabControl">The TabControl hosting the tabs.</param>
        /// <param name="originalSource">The original source of the MouseDown event, typically e.OriginalSource.</param>
        /// <param name="nonClosableTabHeaders">An enumerable of strings representing headers of tabs that should not be closed. Case-sensitive.</param>
        /// <returns>True if a tab was closed, false otherwise.</returns>
        public static bool HandleTabMiddleClickClose(TabControl tabControl, object originalSource, IEnumerable<string>? nonClosableTabHeaders)
        {
            if (tabControl == null || !(originalSource is DependencyObject sourceObject))
            {
                return false;
            }

            var tabItem = FindParent<TabItem>(sourceObject);
            if (tabItem == null)
            {
                return false;
            }

            string? headerString = tabItem.Header?.ToString();

            // Check if the tab is marked as non-closable
            if (nonClosableTabHeaders != null && headerString != null && nonClosableTabHeaders.Contains(headerString))
            {
                return false;
            }

            // Dispose content if applicable
            if (tabItem.Content is IDisposable disposableContent)
            {
                disposableContent.Dispose();
            }
            else if (tabItem.Content is FrameworkElement fe && fe.DataContext is IDisposable disposableDataContext)
            {
                disposableDataContext.Dispose();
            }
            // Note: ImageTabControl has its own Unloaded event for cleanup,
            // which is triggered when it's removed from the visual tree.

            tabControl.Items.Remove(tabItem);
            return true; // Tab was closed
        }
    }
}

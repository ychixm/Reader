using System.Windows;
using System.Windows.Media;

namespace ReaderUtils // Changed namespace
{
    public static class WpfHelpers
    {
        /// <summary>
        /// Finds the first parent of a given DependencyObject that is of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the parent to find.</typeparam>
        /// <param name="child">The starting DependencyObject.</param>
        /// <returns>The first parent of type T, or null if not found.</returns>
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
    }
}

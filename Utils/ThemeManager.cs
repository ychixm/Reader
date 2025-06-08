using System.Windows;
using Utils;

namespace Utils
{
    public static class ThemeManager
    {
        public static void ApplyTheme()
        {
            bool isDarkMode = WindowsThemeHelpers.GetCurrentSystemIsDarkMode();

            if (isDarkMode)
            {
                Application.Current.Resources["TabItemBackgroundBrush"] = Application.Current.Resources["TabItemBackground_Dark"];
                Application.Current.Resources["TabItemBorderBrush"] = Application.Current.Resources["TabItemBorderBrush_Dark"];
                Application.Current.Resources["TabItemForegroundBrush"] = Application.Current.Resources["TabItemForeground_Dark"];
                Application.Current.Resources["TabItemBackground_SelectedBrush"] = Application.Current.Resources["TabItemBackground_Selected_Dark"];
                Application.Current.Resources["TabItemForeground_SelectedBrush"] = Application.Current.Resources["TabItemForeground_Selected_Dark"];
                Application.Current.Resources["TabItemBackground_MouseOverBrush"] = Application.Current.Resources["TabItemBackground_MouseOver_Dark"];
                Application.Current.Resources["TabControlContentBackgroundBrush"] = Application.Current.Resources["TabControlContentBackground_Dark"];
                Application.Current.Resources["PanelBackgroundBrush"] = Application.Current.Resources["PanelBackground_Dark"];
                Application.Current.Resources["MenuBackgroundBrush"] = Application.Current.Resources["MenuBackground_Dark"];
                Application.Current.Resources["MenuItemForegroundBrush"] = Application.Current.Resources["MenuItemForeground_Dark"];
                Application.Current.Resources["MenuSeparatorBackgroundBrush"] = Application.Current.Resources["MenuSeparatorBackground_Dark"];
                // Button Brushes
                Application.Current.Resources["ButtonBackgroundBrush"] = Application.Current.Resources["ButtonBackground_Dark"];
                Application.Current.Resources["ButtonForegroundBrush"] = Application.Current.Resources["ButtonForeground_Dark"];
                Application.Current.Resources["ButtonBorderBrush"] = Application.Current.Resources["ButtonBorder_Dark"];
                Application.Current.Resources["ButtonBackgroundHoverBrush"] = Application.Current.Resources["ButtonBackgroundHover_Dark"];
                // ListItem Brushes
                Application.Current.Resources["ListItemBackgroundBrush"] = Application.Current.Resources["ListItemBackground_Dark"];
                Application.Current.Resources["ListItemForegroundBrush"] = Application.Current.Resources["ListItemForeground_Dark"];
                Application.Current.Resources["ListItemBackgroundHoverBrush"] = Application.Current.Resources["ListItemBackgroundHover_Dark"];
                Application.Current.Resources["ListItemBackgroundSelectedBrush"] = Application.Current.Resources["ListItemBackgroundSelected_Dark"];
                Application.Current.Resources["ListItemForegroundSelectedBrush"] = Application.Current.Resources["ListItemForegroundSelected_Dark"];
                // ScrollViewer Brushes
                Application.Current.Resources["ScrollBarThumbFillBrush"] = Application.Current.Resources["ScrollBarThumbFill_Dark"];
                Application.Current.Resources["ScrollBarTrackBackgroundBrush"] = Application.Current.Resources["ScrollBarTrackBackground_Dark"];
            }
            else
            {
                Application.Current.Resources["TabItemBackgroundBrush"] = Application.Current.Resources["TabItemBackground_Light"];
                Application.Current.Resources["TabItemBorderBrush"] = Application.Current.Resources["TabItemBorderBrush_Light"];
                Application.Current.Resources["TabItemForegroundBrush"] = Application.Current.Resources["TabItemForeground_Light"];
                Application.Current.Resources["TabItemBackground_SelectedBrush"] = Application.Current.Resources["TabItemBackground_Selected_Light"];
                Application.Current.Resources["TabItemForeground_SelectedBrush"] = Application.Current.Resources["TabItemForeground_Selected_Light"];
                Application.Current.Resources["TabItemBackground_MouseOverBrush"] = Application.Current.Resources["TabItemBackground_MouseOver_Light"];
                Application.Current.Resources["TabControlContentBackgroundBrush"] = Application.Current.Resources["TabControlContentBackground_Light"];
                Application.Current.Resources["PanelBackgroundBrush"] = Application.Current.Resources["PanelBackground_Light"];
                Application.Current.Resources["MenuBackgroundBrush"] = Application.Current.Resources["MenuBackground_Light"];
                Application.Current.Resources["MenuItemForegroundBrush"] = Application.Current.Resources["MenuItemForeground_Light"];
                Application.Current.Resources["MenuSeparatorBackgroundBrush"] = Application.Current.Resources["MenuSeparatorBackground_Light"];
                // Button Brushes
                Application.Current.Resources["ButtonBackgroundBrush"] = Application.Current.Resources["ButtonBackground_Light"];
                Application.Current.Resources["ButtonForegroundBrush"] = Application.Current.Resources["ButtonForeground_Light"];
                Application.Current.Resources["ButtonBorderBrush"] = Application.Current.Resources["ButtonBorder_Light"];
                Application.Current.Resources["ButtonBackgroundHoverBrush"] = Application.Current.Resources["ButtonBackgroundHover_Light"];
                // ListItem Brushes
                Application.Current.Resources["ListItemBackgroundBrush"] = Application.Current.Resources["ListItemBackground_Light"];
                Application.Current.Resources["ListItemForegroundBrush"] = Application.Current.Resources["ListItemForeground_Light"];
                Application.Current.Resources["ListItemBackgroundHoverBrush"] = Application.Current.Resources["ListItemBackgroundHover_Light"];
                Application.Current.Resources["ListItemBackgroundSelectedBrush"] = Application.Current.Resources["ListItemBackgroundSelected_Light"];
                Application.Current.Resources["ListItemForegroundSelectedBrush"] = Application.Current.Resources["ListItemForegroundSelected_Light"];
                // ScrollViewer Brushes
                Application.Current.Resources["ScrollBarThumbFillBrush"] = Application.Current.Resources["ScrollBarThumbFill_Light"];
                Application.Current.Resources["ScrollBarTrackBackgroundBrush"] = Application.Current.Resources["ScrollBarTrackBackground_Light"];
            }
        }
    }
}

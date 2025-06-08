using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Reader.UserControls
{
    public partial class OptionsTabContentControl : UserControl
    {
        public OptionsTabContentControl()
        {
            InitializeComponent();
        }

        public void UpdateDisplayedOptions(TabControl sourceTabControl, IEnumerable<string> excludedHeaders)
        {
            if (sourceTabControl == null || OptionsItemsControl == null)
            {
                return;
            }

            OptionsItemsControl.Items.Clear();

            foreach (var item in sourceTabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    string? headerString = tabItem.Header?.ToString();
                    if (headerString == null || (excludedHeaders != null && excludedHeaders.Contains(headerString)))
                    {
                        continue; // Skip excluded tabs or tabs with no header
                    }

                    GroupBox groupBox = new GroupBox
                    {
                        Header = headerString,
                        Margin = new System.Windows.Thickness(5),
                        Padding = new System.Windows.Thickness(5)
                    };

                    // Placeholder content for now
                    TextBlock contentTextBlock = new TextBlock
                    {
                        Text = $"Options for '{headerString}' will be shown here.",
                        TextWrapping = System.Windows.TextWrapping.Wrap
                    };
                    groupBox.Content = contentTextBlock;

                    OptionsItemsControl.Items.Add(groupBox);
                }
            }
        }
    }
}

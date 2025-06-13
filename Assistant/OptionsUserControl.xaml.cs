using System.Windows.Controls;
using System.Collections.Generic; // For IEnumerable
using Utils.Interfaces; // For IOptionsProvider
using System.Windows; // For FrameworkElement

namespace Assistant
{
    public partial class OptionsUserControl : UserControl
    {
        public OptionsUserControl()
        {
            InitializeComponent();
        }

        public void PopulateOptions(IEnumerable<object> tabContents)
        {
            OptionsContainer.Items.Clear(); // Clear previous items

            if (tabContents == null) return;

            foreach (var content in tabContents)
            {
                if (content is IOptionsProvider provider)
                {
                    FrameworkElement optionsView = provider.OptionsControl;
                    if (optionsView != null)
                    {
                        // Optional: Add a header or separator for each module's options
                        // Example:
                        // var moduleName = content.GetType().Name.Replace("UserControl", "");
                        // OptionsContainer.Items.Add(new TextBlock { Text = $"{moduleName} Options", FontWeight = FontWeights.Bold, Margin = new Thickness(0,10,0,5) });
                        OptionsContainer.Items.Add(optionsView);
                    }
                }
            }
        }
    }
}

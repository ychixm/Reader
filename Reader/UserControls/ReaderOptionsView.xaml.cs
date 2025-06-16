using System.Windows.Controls;
using Utils.Models; // For TabOverflowMode
using System; // For Enum
using System.Linq; // For Enum.GetValues

namespace Reader.UserControls
{
    public partial class ReaderOptionsView : UserControl
    {
        public ReaderOptionsView()
        {
            InitializeComponent();
            // Define the Enum values for the ComboBox in XAML.
            // This can also be done in XAML if preferred, but requires an ObjectDataProvider.
            // For simplicity here, we set it if not using a XAML-based resource.
            // However, the XAML provided uses a StaticResource 'TabOverflowModeValues',
            // which needs to be defined in App.xaml or a merged dictionary.
            // We will add this resource in a later step if it doesn't exist.
            // For now, this C# setup is a fallback or alternative.
            if (Resources["TabOverflowModeValues"] == null)
            {
                Resources.Add("TabOverflowModeValues", Enum.GetValues(typeof(TabOverflowMode)).Cast<TabOverflowMode>());
            }
        }
    }
}

using SoundWeaver.Models;
using System.Windows;
using System.Windows.Controls;

namespace SoundWeaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class SoundWeaverControl : UserControl
    {
        public SoundWeaverControl(SoundWeaverControlViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

        }
    }
}

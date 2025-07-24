using System.Windows;
using SoundWeaver.Models;
using SoundWeaver.ViewModels;

namespace SoundWeaver.Vue
{
    public partial class AddSfxWindow : Window
    {
        public SfxElement? CreatedSfx { get; private set; }

        public AddSfxWindow()
        {
            InitializeComponent();
        }

        public AddSfxWindow(AddSfxViewModel vm) : this()
        {
            DataContext = vm;
            vm.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(SfxElement? sfx)
        {
            CreatedSfx = sfx;
            DialogResult = sfx != null;
            Close();
        }
    }
}

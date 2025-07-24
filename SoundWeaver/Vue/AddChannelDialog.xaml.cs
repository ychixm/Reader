using System.Windows;
using SoundWeaver.Models;
using SoundWeaver.ViewModels;

namespace SoundWeaver.Vue
{
    public partial class AddChannelDialogWindow : Window
    {
        public AddChannelDialogWindow()
        {
            InitializeComponent();
            // Branche le handler avec conversion
            if (DataContext is AddChannelDialogViewModel vm)
                vm.RequestClose += (channel) =>
                {
                    this.DialogResultChannel = channel;
                    this.CloseDialog(channel != null); // true si ajout, false si annul
                };
        }
        public ChannelSetting? DialogResultChannel { get; set; }

        public void CloseDialog(bool? result = true)
        {
            if (!IsLoaded || !IsVisible)
            {
                this.Close();
                return;
            }
            this.DialogResult = result;
            this.Close();
        }
    }
}
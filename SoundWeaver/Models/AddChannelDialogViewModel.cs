using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Discord;
using SoundWeaver.Bot;
using SoundWeaver.Models;

namespace SoundWeaver.ViewModels
{
    public class AddChannelDialogViewModel : INotifyPropertyChanged
    {
        private string _guildId = "";
        private string _channelId = "";
        private string _channelName = "";
        private string _bitrate = "64000";

        public string GuildId
        {
            get => _guildId;
            set { _guildId = value; OnPropertyChanged(); }
        }
        public string ChannelId
        {
            get => _channelId;
            set { _channelId = value; OnPropertyChanged(); }
        }
        public string ChannelName
        {
            get => _channelName;
            set { _channelName = value; OnPropertyChanged(); }
        }
        public string Bitrate
        {
            get => _bitrate;
            set { _bitrate = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public event Action<ChannelSetting?>? RequestClose;
        public ChannelSetting? LastValidatedChannel { get; set; }
        public IEnumerable<ChannelSetting> ExistingChannels { get; set; } = Enumerable.Empty<ChannelSetting>();
        public string BotToken { get; set; }

        public AddChannelDialogViewModel()
        {
            SaveCommand = new RelayCommand<object>(_ => Save());
            CancelCommand = new RelayCommand<object>(_ => Cancel());
        }

        private async void Save()
        {
            if (!ulong.TryParse(GuildId, out var gid) ||
                !ulong.TryParse(ChannelId, out var cid) ||
                string.IsNullOrWhiteSpace(BotToken) ||  // Ajoute un champ BotToken pour récupérer le token du bot
                !int.TryParse(Bitrate, out var bitrate))
            {
                MessageBox.Show("Veuillez remplir tous les champs avec des valeurs valides.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Vérifie les doublons
            bool alreadyExists = ExistingChannels.Any(c =>
                c.GuildId == gid && c.ChannelId == cid);

            if (alreadyExists)
            {
                MessageBox.Show("Un salon avec ce Guild ID et ce Channel ID existe déjà.", "Doublon détecté", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Vérifie l'existence via Discord API
            var result = await DiscordBotService.TryResolveChannelNameAsync(BotToken, gid, cid);

            if (!result.Exists)
            {
                MessageBox.Show(result.Error ?? "Salon ou serveur introuvable.", "Erreur Discord", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Prérenseigne le nom si le champ est vide
            var channelName = !string.IsNullOrWhiteSpace(ChannelName) ? ChannelName : result.ChannelName ?? "";

            var newChannel = new ChannelSetting()
            {
                GuildId = gid,
                ChannelId = cid,
                ChannelName = channelName,
                Bitrate = bitrate
            };
            LastValidatedChannel = newChannel;
            RequestClose?.Invoke(newChannel);
        }


        private void Cancel()
        {
            RequestClose?.Invoke(null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}

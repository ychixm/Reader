using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using SoundWeaver;
using SoundWeaver.Models;
using Utils;

public class SoundWeaverOptionsViewModel : IOptionsViewModel, INotifyPropertyChanged
{
    public string Title => "SoundWeaver Options";

    public IReadOnlyList<int> ChannelOptions { get; } = new[] { 1, 2 };

    private int _selectedChannels = 2;
    public int SelectedChannels
    {
        get => _selectedChannels;
        set { if (_selectedChannels != value) { _selectedChannels = value; OnPropertyChanged(); } }
    }

    // Liste observable de tous les salons connus
    public ObservableCollection<ChannelSetting> ChannelBitrateSettings { get; } = new();

    private ChannelSetting _selectedChannelBitrateSetting;
    public ChannelSetting SelectedChannelBitrateSetting
    {
        get => _selectedChannelBitrateSetting;
        set { _selectedChannelBitrateSetting = value; OnPropertyChanged(); }
    }

    private string _discordToken;
    public string DiscordToken
    {
        get => _discordToken;
        set { _discordToken = value; OnPropertyChanged(); }
    }

    public SoundWeaverOptionsViewModel()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        var settings = AppSettingsService.LoadModuleSettings(
                           "SoundWeaver",
                           () => new SoundWeaverSettings());

        SelectedChannels = settings.SelectedChannels is 1 or 2
                           ? settings.SelectedChannels
                           : 2;

        ChannelBitrateSettings.Clear();
        if (settings.ChannelSettings != null)
        {
            foreach (var item in settings.ChannelSettings)
                ChannelBitrateSettings.Add(item);
            SelectedChannelBitrateSetting = ChannelBitrateSettings.FirstOrDefault();
        }
        DiscordToken = settings.DiscordToken ?? "";
    }

    public void SaveSettings()
    {
        var settings = AppSettingsService.LoadModuleSettings(
            "SoundWeaver", () => new SoundWeaverSettings());
        settings.SelectedChannels = this.SelectedChannels;
        settings.ChannelSettings = ChannelBitrateSettings.ToList();
        settings.DiscordToken = this.DiscordToken;
        AppSettingsService.SaveModuleSettings("SoundWeaver", settings);
    }

    /// <summary>
    /// Ajoute ou met à jour un salon vocal (appelé lors de la connexion ou d’un scan).
    /// </summary>
    public ChannelSetting RegisterOrUpdateChannel(ulong channelId, string channelName, int discordCap)
    {
        var found = ChannelBitrateSettings.FirstOrDefault(x => x.ChannelId == channelId);
        if (found == null)
        {
            found = new ChannelSetting
            {
                ChannelId = channelId,
                ChannelName = channelName,
                DiscordBitrateCap = discordCap,
                Bitrate = Math.Min(64000, discordCap)
            };
            ChannelBitrateSettings.Add(found);
        }
        else
        {
            found.ChannelName = channelName;
            if (found.DiscordBitrateCap != discordCap)
            {
                found.DiscordBitrateCap = discordCap;
                if (found.Bitrate > discordCap)
                    found.Bitrate = discordCap;
            }
        }
        SelectedChannelBitrateSetting = found;
        SaveSettings();
        return found;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    public UserControl GetView()
    {
        var view = new SoundWeaverOptionsView();
        view.DataContext = this;
        return view;
    }

    public void Apply()
    {
        SaveSettings();
    }
}
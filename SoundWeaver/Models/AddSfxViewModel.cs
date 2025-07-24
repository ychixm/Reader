using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using SoundWeaver;
using SoundWeaver.Models;

public class AddSfxViewModel : BaseViewModel
{
    public ObservableCollection<SfxType> Types { get; } = new ObservableCollection<SfxType> { SfxType.Instant, SfxType.Continuous };

    private string _filePath = "";
    public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); UpdateCanConfirm(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); UpdateCanConfirm(); } }
    private string _displayName = "";

    private SfxType _selectedType = SfxType.Instant;
    public SfxType SelectedType { get => _selectedType; set { _selectedType = value; OnPropertyChanged(); } }

    public ICommand BrowseFileCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action<SfxElement?>? RequestClose;

    private bool _canConfirm;
    public bool CanConfirm { get => _canConfirm; private set { _canConfirm = value; OnPropertyChanged(); } }

    public AddSfxViewModel()
    {
        BrowseFileCommand = new RelayCommand<object>(_ => BrowseFile());
        ConfirmCommand = new RelayCommand<object>(_ => Confirm(), _ => CanConfirm);
        CancelCommand = new RelayCommand<object>(_ => Cancel());
    }

    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Fichiers audio|*.mp3;*.wav;*.ogg;*.flac;*.aac",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void Confirm()
    {
        if (!CanConfirm) return;
        var sfx = new SfxElement
        {
            FilePath = FilePath,
            DisplayName = DisplayName,
            Type = SelectedType,
            Volume = 1.0
        };
        RequestClose?.Invoke(sfx);
    }

    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    private void UpdateCanConfirm()
    {
        CanConfirm = !string.IsNullOrWhiteSpace(FilePath) && !string.IsNullOrWhiteSpace(DisplayName);
    }
}

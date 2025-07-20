using System.Windows.Controls;
using SoundWeaver.Models;
using Utils;

namespace SoundWeaver
{
    public class SoundWeaverSubApplication : ISubApplication
    {
        private readonly ILoggerService _logger;
        private SoundWeaverControl? _mainView;
        private SoundWeaverControlViewModel? _viewModel; // Ajouté : singleton du VM principal
        private SoundWeaverOptionsViewModel? _optionsViewModel;
        private SoundWeaverSettings _settings;

        public string Name => "SoundWeaver";

        public SoundWeaverSubApplication(ILoggerService logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());
            _logger.LogInfo("SoundWeaverSubApplication initialized.");
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                // Instancie ou récupère le ViewModel unique (peut aussi venir de DI)
                _viewModel ??= new SoundWeaverControlViewModel();

                _mainView = new SoundWeaverControl(_viewModel);
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                _optionsViewModel = new SoundWeaverOptionsViewModel();
            }
            return _optionsViewModel;
        }

        public void ApplyOptions()
        {
            _optionsViewModel?.Apply();

            _settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());

            if (_mainView != null && _viewModel != null)
            {
                _logger.LogInfo("SoundWeaverSubApplication: ApplyOptions called. Settings reloaded.");
            }
        }

        public void Shutdown()
        {
            // Séquence de nettoyage propre
            if (_viewModel != null)
            {
                try
                {
                    _viewModel.DisconnectBotCommand.Execute(null);
                }
                catch { }
                _viewModel.Dispose();
            }
        }
    }
}

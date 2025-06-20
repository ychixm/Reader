using System.Windows.Controls;
using Utils; // Required for ISubApplication and IOptionsViewModel

namespace Assistant.TemplateProject
{
    public class TemplateSubApplication : ISubApplication
    {
        private TemplateUserControl _mainControl;
        private TemplateOptionsViewModel _optionsViewModel;
        private TemplateOptionsView _optionsView;

        public string ApplicationName => "Template Application";

        public string ApplicationId => "TemplateApplication"; // Unique ID for this sub-application

        public UserControl MainUserControl
        {
            get
            {
                if (_mainControl == null)
                    _mainControl = new TemplateUserControl();
                return _mainControl;
            }
        }

        public IOptionsViewModel OptionsViewModel
        {
            get
            {
                if (_optionsViewModel == null)
                    _optionsViewModel = new TemplateOptionsViewModel();
                return _optionsViewModel;
            }
        }

        public UserControl OptionsUserControl
        {
            get
            {
                if (_optionsView == null)
                {
                    _optionsView = new TemplateOptionsView();
                    _optionsView.DataContext = OptionsViewModel; // Set DataContext
                }
                return _optionsView;
            }
        }

        public void LoadSettings(string json)
        {
            OptionsViewModel.Load(json);
        }

        public string SaveSettings()
        {
            return OptionsViewModel.Save();
        }

        public void Activate()
        {
            // Logic to run when the sub-application becomes active
            // For example, refresh data, start timers, etc.
        }

        public void Deactivate()
        {
            // Logic to run when the sub-application becomes inactive
            // For example, save state, stop timers, etc.
        }
    }
}

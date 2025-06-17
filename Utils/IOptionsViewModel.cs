using System.ComponentModel;
using System.Windows.Controls;

namespace Utils
{
    public interface IOptionsViewModel : INotifyPropertyChanged
    {
        string Title { get; }
        UserControl GetView();
        void LoadSettings();
        void Apply();
    }
}

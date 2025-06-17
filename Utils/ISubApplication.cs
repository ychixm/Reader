using System.Windows.Controls;

namespace Utils
{
    public interface ISubApplication
    {
        string Name { get; }
        UserControl GetMainView();
        IOptionsViewModel GetOptionsViewModel();
        void ApplyOptions();
    }
}

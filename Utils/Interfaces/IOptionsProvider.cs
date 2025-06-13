using System.Windows; // For FrameworkElement

namespace Utils.Interfaces // Assuming a new Interfaces folder, or directly in Utils if preferred
{
    public interface IOptionsProvider
    {
        FrameworkElement OptionsControl { get; }
    }
}

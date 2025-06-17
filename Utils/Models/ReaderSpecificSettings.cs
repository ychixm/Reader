using Utils.Models; // For NavigationMethod

namespace Utils.Models
{
    public class ReaderSpecificSettings
    {
        public string? DefaultPath { get; set; }
        public string? DefaultTabOverflowMode { get; set; } // Assuming TabOverflowMode will be handled as string or a separate enum later
        public NavigationMethod EnabledNavigationMethods { get; set; } = NavigationMethod.All;
    }
}

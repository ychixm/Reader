using Reader.Models; // For NavigationMethod enum

namespace Reader.Models
{
    public class AppSettings
    {
        public string DefaultPath { get; set; }
        public string DefaultTabOverflowMode { get; set; }
        public NavigationMethod EnabledNavigationMethods { get; set; } = NavigationMethod.All;
    }
}

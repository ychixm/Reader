namespace Reader.Models
{
    public class ReaderSettings
    {
        public string? DefaultPath { get; set; }
        public string? DefaultTabOverflowMode { get; set; }
        public NavigationMethod EnabledNavigationMethods { get; set; } = NavigationMethod.All;
    }
}

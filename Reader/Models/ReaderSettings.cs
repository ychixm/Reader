// No using Utils.Models needed here for NavigationMethod anymore.
// using Reader.Models; // Implicitly part of this namespace.

namespace Reader.Models
{
    public class ReaderSettings
    {
        public string? DefaultPath { get; set; }
        public string? DefaultTabOverflowMode { get; set; } // This type (string) is fine. If TabOverflowMode enum is used, its location matters.
                                                              // Assuming DefaultTabOverflowMode stores the string representation of Utils.Models.TabOverflowMode
        public NavigationMethod EnabledNavigationMethods { get; set; } = NavigationMethod.All;
    }
}

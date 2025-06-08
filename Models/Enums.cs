namespace Reader.Models
{
    public enum TabOverflowMode
    {
        Scrollbar,
        ArrowButtons,
        TabDropdown
    }

    [Flags]
    public enum NavigationMethod
    {
        None = 0,
        KeyboardArrows = 1,
        GridClick = 2,
        VisibleButtons = 4,
        All = KeyboardArrows | GridClick | VisibleButtons
    }
}

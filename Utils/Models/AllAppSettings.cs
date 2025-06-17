namespace Utils.Models
{
    public class AllAppSettings
    {
        public ReaderSpecificSettings Reader { get; set; } = new ReaderSpecificSettings();
        // Future settings for other modules will go here, e.g.:
        // public AssistantSettings Assistant { get; set; } = new AssistantSettings();
    }
}

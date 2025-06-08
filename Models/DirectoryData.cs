using System.IO;

namespace Reader.Models
{
    /// <summary>
    /// Represents data associated with a directory/chapter.
    /// </summary>
    public class DirectoryData
    {
        /// <summary>
        /// Gets the directory information.
        /// </summary>
        public DirectoryInfo DirectoryInfo { get; }

        /// <summary>
        /// Gets or sets the display name for the directory.
        /// Defaults to the directory's actual name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets a list of tags associated with the directory.
        /// Initialized to an empty list.
        /// </summary>
        public List<string> Tags { get; set; }

        public DirectoryData(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo ?? throw new ArgumentNullException(nameof(directoryInfo));
            DisplayName = directoryInfo?.Name ?? string.Empty;
            Tags = new List<string>();
        }
    }
}

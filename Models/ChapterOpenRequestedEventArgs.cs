using System;
using System.Collections.Generic;

namespace Reader.Models
{
    public class ChapterOpenRequestedEventArgs : EventArgs
    {
        public string DirectoryPath { get; }
        public List<string> ImagePaths { get; }
        public bool SwitchToTab { get; }

        public ChapterOpenRequestedEventArgs(string directoryPath, List<string> imagePaths, bool switchToTab)
        {
            DirectoryPath = directoryPath;
            ImagePaths = imagePaths;
            SwitchToTab = switchToTab;
        }
    }
}

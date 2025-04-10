using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reader.Models
{
    public class DirectoryData(DirectoryInfo directoryInfo)
    {
        public DirectoryInfo DirectoryInfo { get; set; } = directoryInfo;
        public string DisplayName { get; set; }
        public List<string> Tags { get; set; }
    }
}

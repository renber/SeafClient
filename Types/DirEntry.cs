using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Types
{
    /// <summary>
    /// Entry of a directory (file or directory)
    /// </summary>
    public class DirEntry
    {
        public string ID { get; set; }
        public DirEntryType Type { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// File size (only if Type is File)
        /// </summary>
        public long Size { get; set; }
    }

    public enum DirEntryType
    {
        File,
        Dir
    }
}

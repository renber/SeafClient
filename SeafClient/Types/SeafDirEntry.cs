using Newtonsoft.Json;
using SeafClient.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Types
{
    /// <summary>
    /// Entry of a directory (file or directory)
    /// </summary>
    public class SeafDirEntry
    {
        public string ID { get; set; }

        public String LibraryId { get; set; }

        public DirEntryType Type { get; set; }        

        public string Name { get; set; }

        /// <summary>
        /// Time of the last modification of this entry
        /// (as UNIX timestamp)
        /// </summary>
        [JsonProperty("mtime"), JsonConverter(typeof(SeafTimestampConverter))]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// File size (only if Type is File)
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// The path of this item in its library
        /// </summary>
        public string Path { get; set; }
    }

    public enum DirEntryType
    {
        File,        
        Dir
    }
}

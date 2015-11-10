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
        public virtual string Id { get; set; }

        [JsonIgnore]
        public virtual String LibraryId { get; set; }

        public virtual DirEntryType Type { get; set; }        

        public virtual string Name { get; set; }

        /// <summary>
        /// Time of the last modification of this entry
        /// (as UNIX timestamp)
        /// </summary>
        [JsonProperty("mtime"), JsonConverter(typeof(SeafTimestampConverter))]
        public virtual DateTime Timestamp { get; set; }

        /// <summary>
        /// File size (only if Type is File)
        /// </summary>
        public virtual long Size { get; set; }        

        /// <summary>
        /// The path of this item in its library
        /// </summary>        
        [JsonIgnore]
        public virtual string Path { get; set; }
    }

    public enum DirEntryType
    {
        File,        
        Dir
    }
}

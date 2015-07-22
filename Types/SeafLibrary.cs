using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Types
{
    /// <summary>
    /// Represents a seafile library
    /// </summary>
    public class SeafLibrary
    {
        /// <summary>
        /// The unique ID of this seafile library / repository
        /// </summary>
        public string Id { get; set; }
        public string Name { get; set; }

        public string Owner { get; set; }

        public bool Encrypted { get; set; }

        /// <summary>
        /// Time of the last modification of this entry
        /// (as UNIX timestamp)
        [JsonProperty("mtime")]        
        public long Timestamp { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; }
    }
}

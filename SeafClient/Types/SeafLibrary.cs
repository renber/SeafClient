using Newtonsoft.Json;
using SeafClient.Converters;
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
        public virtual string Id { get; set; }
        public virtual string Name { get; set; }

        public virtual string Owner { get; set; }

        public virtual bool Encrypted { get; set; }

        [JsonConverter(typeof(SeafPermissionConverter))]
        public virtual SeafPermission Permission { get; set; }

        /// <summary>
        /// Time of the last modification of this entry
        /// (as UNIX timestamp)
        [JsonProperty("mtime"), JsonConverter(typeof(SeafTimestampConverter))]        
        public virtual DateTime Timestamp { get; set; }

        [JsonProperty("desc")]
        public virtual string Description { get; set; }
    }
}

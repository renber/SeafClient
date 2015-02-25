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
        public string Id { get; set; }
        public string Name { get; set; }

        public string Owner { get; set; }

        [JsonProperty("mtime")]
        public int Timestamp { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; }
    }
}

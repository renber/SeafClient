using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.ResponseTypes
{
    /// <summary>
    /// Represents a seafile library
    /// </summary>
    public class Library
    {
        public string ID { get; set; }
        public string Name { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; }
    }
}

using Newtonsoft.Json;
using SeafClient.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Types
{
    /// <summary>
    /// Represents a shared library
    /// </summary>
    public class SeafSharedLibrary : SeafLibrary
    {
        [JsonProperty("repo_id")]
        public override string Id { get; set; }

        [JsonProperty("repo_name")]
        public override string Name { get; set; }

        [JsonProperty("repo_desc")]
        public override string Description { get; set; }

        [JsonProperty("user")]
        public override string Owner { get; set; }

        [JsonProperty("last_modified"), JsonConverter(typeof(SeafTimestampConverter))]
        public override DateTime Timestamp { get; set; }
    }
}

using Newtonsoft.Json;
using SeafClient.Converters;
using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to get a list of all starred files of the session owner
    /// </summary>
    public class ListStarredFilesRequest : SessionRequest<List<SeafDirEntry>>
    {
        public override string CommandUri
        {
            get
            {
                return "api2/starredfiles/";
            }
        }

        public ListStarredFilesRequest(string authToken) 
            : base(authToken)
        {
            // --
        }

        public override async Task<List<SeafDirEntry>> ParseResponseAsync(HttpResponseMessage msg)
        {
            // try to read the response content as JSON object
            var stream = await msg.Content.ReadAsStreamAsync();
            using (StreamReader sr = new StreamReader(stream))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new JsonSerializer();
                var files = serializer.Deserialize<List<SeafStarredFile>>(reader);
                // set the filenames
                foreach (var f in files)
                    f.Name = Path.GetFileName(f.Path);
                return files.OfType<SeafDirEntry>().ToList();
            }
        }
    }

    /// <summary>
    /// Starred file class
    /// (which is essentially a SeafDirEntry but seafile
    /// uses different field names in its json response)
    /// </summary>
    class SeafStarredFile : SeafDirEntry
    {        
        [JsonProperty("repo")]
        public override String LibraryId { get; set; }

        [JsonProperty("dir"), JsonConverter(typeof(SeafEntryTypeConverter))]
        public override DirEntryType Type { get; set; }                        

        /// <summary>
        /// The path of this item in its library
        /// </summary>        
        public override string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
            }
        }

        // hidden property to allow deserialization of the path field
        // because we cannot easily remove the JsonIgnore attribute
        // from the base class's Path property
        // but we also don't want to change the base class (see http://www.newtonsoft.com/json/help/html/ConditionalProperties.htm)
        [JsonProperty("path")]
        protected string _path { get; set; }
    }
}

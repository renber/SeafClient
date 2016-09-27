using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Libraries
{
    /// <summary>
    /// Request to return the id of the user's default library
    /// </summary>
    public class GetDefaultLibraryRequest : SessionRequest<SimpleLibraryResult>
    {
        public override string CommandUri
        {
            get { return "api2/default-repo/"; }
        }

        public GetDefaultLibraryRequest(string authToken)
            : base(authToken)
        {
            // --
        }
    }

    public class SimpleLibraryResult
    {
        [JsonProperty("repo_id")]
        public string LibraryId { get; set; }

        public bool Exists { get; set; }
    }
}

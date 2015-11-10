using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request which stars a file (mark it as favorite)
    /// </summary>
    public class StarFileRequest : SessionRequest<bool>
    {
        public string LibraryId { get; private set; }
        public string Path { get; private set; }

        public override string CommandUri
        {
            get
            {
                return "api2/starredfiles/";
            }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get
            {
                return HttpAccessMethod.Post;
            }
        }

        public StarFileRequest(string authToken, string libraryId, string path)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            var parameters = new List<KeyValuePair<string, string>>();
            parameters.AddRange(base.GetPostParameters());

            parameters.Add(new KeyValuePair<string, string>("repo_id", LibraryId));
            parameters.Add(new KeyValuePair<string, string>("p", Path));

            return parameters;
        }

        public override bool WasSuccessful(HttpResponseMessage msg)
        {
            return msg.StatusCode == HttpStatusCode.Created;
        }

        public override async Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string r = await msg.Content.ReadAsStringAsync();
            return r == "\"success\"";
        }
    }
}

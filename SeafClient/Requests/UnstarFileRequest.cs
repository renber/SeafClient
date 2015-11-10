using SeafClient.Utils;
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
    /// Request to unstar an already starred file
    /// </summary>
    public class UnstarFileRequest : SessionRequest<bool>
    {
        public string LibraryId { get; private set; }
        public string Path { get; private set; }

        public override HttpAccessMethod HttpAccessMethod
        {
            get
            {
                return HttpAccessMethod.Delete;
            }
        }        

        public override string CommandUri
        {
            get
            {
                return String.Format("api2/starredfiles/?repo_id={0}&p={1}", LibraryId, WebUtility.UrlEncode(Path));
            }
        }

        public UnstarFileRequest(string authToken, string libraryId, string path) 
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;
        }

        public override bool WasSuccessful(HttpResponseMessage msg)
        {
            return msg.StatusCode == HttpStatusCode.OK;
        }

        public override async System.Threading.Tasks.Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }
    }
}

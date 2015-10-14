using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SeafClient.Requests
{
    public class CreateDirectoryRequest : SessionRequest<bool>
    {
        public string LibraryId { get; set; }

        public string Path { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/dir/?p={1}", LibraryId, Path); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Post; }
        }

        public CreateDirectoryRequest(string authToken, string libraryId, string path)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            foreach (var p in base.GetPostParameters())
                yield return p;

            yield return new KeyValuePair<string, string>("operation", "mkdir");
        }

        public override bool WasSuccessful(System.Net.Http.HttpResponseMessage msg)
        {
            return msg.StatusCode == HttpStatusCode.Created;
        }

        public override async System.Threading.Tasks.Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }

        public override SeafError GetSeafError(HttpResponseMessage msg)
        {
            switch (msg.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    return new SeafError(msg.StatusCode, SeafErrorCode.PathDoesNotExist);                    
                default:
                    return base.GetSeafError(msg);
            }            
        }
    }
}

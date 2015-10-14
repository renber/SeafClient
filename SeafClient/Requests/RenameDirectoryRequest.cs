using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SeafClient.Requests
{
    public class RenameDirectoryRequest : SessionRequest<bool>
    {
        public string LibraryId { get; set; }

        public string Path { get; set; }

        public String NewDirectoryName { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/dir/?p={1}", LibraryId, Path); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Post; }
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            foreach (var p in base.GetPostParameters())
                yield return p;

            yield return new KeyValuePair<string, string>("operation", "rename");
            yield return new KeyValuePair<string, string>("newname", NewDirectoryName);
        }

        public RenameDirectoryRequest(string authToken, string libraryId, string path, string newDirectoryName)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;
            NewDirectoryName = newDirectoryName;
        }

        public override async System.Threading.Tasks.Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }

        public override SeafError GetSeafError(System.Net.Http.HttpResponseMessage msg)
        {
            if (msg.StatusCode == HttpStatusCode.Forbidden)
            {
                return new SeafError(msg.StatusCode, SeafErrorCode.NotEnoughPermissions);                
            } else
                return base.GetSeafError(msg);
        }
    }
}

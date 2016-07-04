using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SeafClient.Requests
{
    public class CopyFileRequest : SessionRequest<bool>
    {
        public string LibraryId { get; set; }

        public string Path { get; set; }

        public string TargetLibraryId { get; set; }

        public string TargetDirectory { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/file/?p={1}", LibraryId, Path); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Post; }
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            foreach (var p in base.GetPostParameters())
                yield return p;

            yield return new KeyValuePair<string, string>("operation", "copy");
            yield return new KeyValuePair<string, string>("dst_repo", TargetLibraryId);
            yield return new KeyValuePair<string, string>("dst_dir", TargetDirectory);
        }

        public CopyFileRequest(string authToken, string libraryId, string path, string targetLibraryId, string targetDirectory)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;
            TargetLibraryId = targetLibraryId;
            TargetDirectory = targetDirectory;
        }

        public override bool WasSuccessful(System.Net.Http.HttpResponseMessage msg)
        {            
            if (msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                // there seems to be a bug in the seafile web api
                // as the server returns NotFound even when the renaming was successful
                return true;

            return msg.StatusCode == HttpStatusCode.OK
                || msg.StatusCode == HttpStatusCode.Redirect
                || msg.StatusCode == HttpStatusCode.MovedPermanently;
        }

        public override async System.Threading.Tasks.Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();

            if (content.Contains("not found"))
                // there seems to be a bug in the seafile web api
                // as the server returns NotFound even when the renaming was successful
                return true;
            else
                return content == "\"success\"";
        }

        public override SeafError GetSeafError(System.Net.Http.HttpResponseMessage msg)
        {
            if (msg.StatusCode == HttpStatusCode.Forbidden)
            {
                return new SeafError(msg.StatusCode, SeafErrorCode.NotEnoughPermissions);                
            }
            else
                return base.GetSeafError(msg);
        }
    }
}

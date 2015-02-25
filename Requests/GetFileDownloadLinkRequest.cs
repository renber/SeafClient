using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Return a download link for the given file
    /// </summary>
    public class GetFileDownloadLinkRequest : SessionRequest<string>
    {
        public string LibraryId { get; set; }

        public string Path { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/file/?p={1}", LibraryId, Path); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Get; }
        }

        public GetFileDownloadLinkRequest(string authToken, string libraryId, string path)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;
        }

        public override string GetErrorDescription(HttpResponseMessage msg)
        {
            switch (msg.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return "File not found";
                case HttpStatusCode.BadRequest:
                    return "Path is missing or invalid";
                default:
                    return base.GetErrorDescription(msg);
            }
        }

        public override async System.Threading.Tasks.Task<string> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content;
        }
    }
}

using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SeafClient.Types;
using System.Net.Http;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to delete a directory or file in a library
    /// </summary>
    public class DeleteDirEntryRequest : SessionRequest<bool>
    {
        public string LibraryId { get; set; }

        public string Path { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/dir/?p={1}", LibraryId, WebUtility.UrlEncode(Path)); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Delete; }
        }

        public DeleteDirEntryRequest(string authToken, string libraryId, string path)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;
        }

        public override async Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }

        public override SeafError GetSeafError(HttpResponseMessage msg)
        {
            if (msg.StatusCode == HttpStatusCode.BadRequest)
                return new SeafError(msg.StatusCode, SeafErrorCode.PathDoesNotExist);

            else
                return base.GetSeafError(msg);
        }
    }
}

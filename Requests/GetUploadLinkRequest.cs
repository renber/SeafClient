using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request a link which can be used to upload files with
    /// </summary>
    public class GetUploadLinkRequest : SessionRequest<string>
    {
        public String LibraryId { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/upload-link/", LibraryId); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Get; }
        }

        public GetUploadLinkRequest(string authToken, string libraryId)
            : base(authToken)
        {
            LibraryId = libraryId;
        }

        public override string GetErrorDescription(System.Net.Http.HttpResponseMessage msg)
        {
            switch ((int)msg.StatusCode)
            {
                case 500:
                    return "Run out of quota";
                default:
                    return base.GetErrorDescription(msg);
            }            
        }

        public override async System.Threading.Tasks.Task<string> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string r = await msg.Content.ReadAsStringAsync();
            return r.Trim('\"');
        }
    }
}

using SeafClient.Types;
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

        public GetUploadLinkRequest(string authToken, string libraryId)
            : base(authToken)
        {
            LibraryId = libraryId;
        }

        public override SeafError GetSeafError(System.Net.Http.HttpResponseMessage msg)
        {
            switch (msg.StatusCode)
            {
                case System.Net.HttpStatusCode.InternalServerError:
                    return new SeafError(msg.StatusCode, SeafErrorCode.OutOfQuota);                    
                default:
                    return base.GetSeafError(msg);
            }            
        }

        public override async System.Threading.Tasks.Task<string> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string r = await msg.Content.ReadAsStringAsync();
            return r.Trim('\"');
        }
    }
}

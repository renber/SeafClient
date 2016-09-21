using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{    

    /// <summary>
    /// Request a link which can be used to update an existing file
    /// </summary>
    public class GetUpdateLinkRequest : GetUploadLinkRequest
    {
        public string TargetDirectory { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/update-link/?p={1}", LibraryId, TargetDirectory); }
        }

        public GetUpdateLinkRequest(string authToken, string libraryId, string targetDirectory)
            : base(authToken, libraryId)
        {
            if (String.IsNullOrEmpty(targetDirectory))
                targetDirectory = "/";

            TargetDirectory = targetDirectory;
        }        
    }
}

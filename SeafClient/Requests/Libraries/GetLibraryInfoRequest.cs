using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Libraries
{
    /// <summary>
    /// Returns the library information for a given library id
    /// </summary>
    public class GetLibraryInfoRequest : SessionRequest<SeafLibrary>
    {
        public string LibraryId { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/", LibraryId); }
        }

        public GetLibraryInfoRequest(string authToken, string libraryId)
            : base(authToken)
        {
            LibraryId = libraryId;
        }
    }
}

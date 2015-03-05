using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to receive the contents of a directory
    /// </summary>
    public class ListDirectoryEntriesRequest : SessionRequest<List<SeafDirEntry>>
    {
        public string LibraryId { get; set; }

        public String Path { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/dir/?p={1}", LibraryId, Path); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Get; }
        }

        public ListDirectoryEntriesRequest(string authToken, string libraryId, string path)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = path;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;
        }
    }
}

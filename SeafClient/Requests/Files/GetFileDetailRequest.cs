using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace SeafClient.Requests.Files
{
    /// <summary>
    /// Request to retrieve details about a file in a library
    /// </summary>
    public class GetFileDetailRequest : SessionRequest<SeafDirEntry>
    {
        public string LibraryId { get; set; }
        public String Path { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/file/detail/?p={1}", LibraryId, Path); }
        }

        public GetFileDetailRequest(string authToken, string libraryId, string path) 
            : base(authToken)
        {            
            LibraryId = libraryId;
            Path = path;
        }

        public override async Task<SeafDirEntry> ParseResponseAsync(HttpResponseMessage msg)
        {
            SeafDirEntry entry = await base.ParseResponseAsync(msg);

            // set the library id &  path of the item           
            entry.LibraryId = LibraryId;
            entry.Path = Path;

            return entry;
        }
    }
}

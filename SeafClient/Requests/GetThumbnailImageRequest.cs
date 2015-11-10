using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Requestto retrieve  a thumbnail for the given image
    /// </summary>
    public class GetThumbnailImageRequest : SessionRequest<byte[]>
    {
        public string LibraryId { get; set; }

        public String Path { get; set; }

        public int ThumbnailSize { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/repos/{0}/thumbnail/?p={1}&size={2:d}", LibraryId, WebUtility.UrlEncode(Path), ThumbnailSize); }
        }

        public override async Task<byte[]> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            return await msg.Content.ReadAsByteArrayAsync();            
        }

        public GetThumbnailImageRequest(string authToken, string libraryId, string imageFilePath, int thumbnailSize)
            : base(authToken)
        {
            LibraryId = libraryId;
            Path = imageFilePath;
            ThumbnailSize = thumbnailSize;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;
        }
    }
}

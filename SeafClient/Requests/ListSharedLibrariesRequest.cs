using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SeafClient.Converters;

namespace SeafClient.Requests
{
    public class ListSharedLibrariesRequest : SessionRequest<IList<SeafSharedLibrary>>
    {
        public override string CommandUri
        {
            get { return "api2/shared-repos/"; }
        }

        public ListSharedLibrariesRequest(string authToken)
            : base(authToken)
        {
            // --
        }

        public override async Task<IList<SeafSharedLibrary>> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();

            // the server responds with SeafLibrary objects but
            // the field names are different than with the ListLibraryRequest
            // so use the SeafSharedLibrary class to translate 
            return JsonConvert.DeserializeObject<IList<SeafSharedLibrary>>(content);            
        }
    }
}

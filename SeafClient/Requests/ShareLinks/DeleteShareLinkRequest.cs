using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.ShareLinks
{
    public class DeleteShareLinkRequest : SessionRequest<bool>
    {
        public string ShareLinkToken { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api/v2.1/share-links/{0}/", ShareLinkToken); }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Delete; }
        }

        public DeleteShareLinkRequest(string authToken, string pShareLinkToken)
        : base(authToken)
        {
            ShareLinkToken = pShareLinkToken;
        }

        public override async Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }


    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    class DeleteGroupRequest : SessionRequest<bool>
    {
        public override string CommandUri => String.Format("api2/groups/{0:d}/", GroupId);

        public override HttpAccessMethod HttpAccessMethod => HttpAccessMethod.Delete;

        public int GroupId { get; set; }

        public DeleteGroupRequest(String authToken, int groupId)
            : base (authToken)
        {
            GroupId = groupId;
        }

        public override async Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }
    }
}

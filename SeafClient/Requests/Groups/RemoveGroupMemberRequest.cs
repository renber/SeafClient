using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    public class RemoveGroupMemberRequest : SessionRequest<bool>
    {
        public override string CommandUri => String.Format("api2/groups/{0:d}/members/", GroupId);

        public override HttpAccessMethod HttpAccessMethod => HttpAccessMethod.Delete;

        public int GroupId { get; set; }

        public string UserName { get; set; }

        public RemoveGroupMemberRequest(string authToken, int groupId, string userName)
            : base(authToken)
        {
            GroupId = groupId;
            UserName = userName;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetBodyParameters()
        {
            yield return new KeyValuePair<string, string>("user_name", UserName);
        }

        public override async Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"success\"";
        }
    }
}

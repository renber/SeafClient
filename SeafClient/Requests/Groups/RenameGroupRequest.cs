using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    /// <summary>
    /// Request to rename a group
    /// </summary>
    public class RenameGroupRequest : SessionRequest<bool>
    {
        public override string CommandUri => String.Format("api2/groups/{0:d}/", GroupId);

        public override HttpAccessMethod HttpAccessMethod => HttpAccessMethod.Post;

        public int GroupId { get; set; }

        public string NewName { get; set; }

        public RenameGroupRequest(String authToken, int groupId, string newName)
            : base(authToken)
        {
            GroupId = groupId;
            NewName = newName ?? "";
        }

        public override IEnumerable<KeyValuePair<string, string>> GetBodyParameters()
        {
            yield return new KeyValuePair<string, string>("operation", "rename");
            yield return new KeyValuePair<string, string>("newname", NewName);
        }

        public override Task<bool> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            return Task.FromResult(msg.IsSuccessStatusCode);
        }
    }    
}

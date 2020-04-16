using Newtonsoft.Json;
using SeafClient.Requests.UserAccountInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    /// <summary>
    /// Request to add multiple users to a group at once
    /// Result is a detailed list of addition success/failure information for each user
    /// 
    /// Requires at least Seafile Server Version v5.1.0 as it uses the new API 2.1v
    /// </summary>
    public class BulkAddGroupMemberRequest : SessionRequest<BulkAddGroupMemberResponse>
    {
        public override string CommandUri => String.Format("api/v2.1/groups/{0:d}/members/bulk/", GroupId);

        public override HttpAccessMethod HttpAccessMethod => HttpAccessMethod.Post;

        public int GroupId { get; set; }

        public IEnumerable<string> UserNames { get; set; }
        private string Emails =>  UserNames == null ? "" : String.Join(",", UserNames);

        public BulkAddGroupMemberRequest(string authToken, int groupId, IEnumerable<string> userNames) 
            : base(authToken)
        {
            GroupId = groupId;
            if (userNames == null)
            {
				throw new ArgumentNullException(nameof(userNames));
	        }
            UserNames = userNames;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetBodyParameters()
        {
            yield return new KeyValuePair<string, string>("emails", Emails);
        }

        public override bool SupportedWithServerVersion(Version version)
        {            
            /// Requires at least Seafile Server Version v5.1.0 as it uses the new API 2.1v
            return version >= new Version(5, 1, 0);
        }

    }

    /// <summary>
    /// Contains information in the result state for each individual user which
    /// should be added to a group
    /// </summary>
    public class BulkAddGroupMemberResponse
    {
        public FailedAddGroupMember[] Failed { get; set; }
        public SuccessAddGroupMember[] Success { get; set; }
    }

    /// <summary>
    /// Indicates the given user could not be added to the group
    /// and the reason
    /// </summary>
    public class FailedAddGroupMember
    {
        [JsonProperty("error_msg")]
        public string ErrorMessage { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// Indicates that the given user was successfully added
    /// to the group
    /// </summary>
    public class SuccessAddGroupMember
    {
        [JsonProperty("login_id")]
        public string LoginId { get; set; }

        public string Name { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("is_admin")]
        public bool IsAdmin { get; set; }

        [JsonProperty("contact_email")]
        public string ContactEmail { get; set; }

        public string Email { get; set; }
    }

}

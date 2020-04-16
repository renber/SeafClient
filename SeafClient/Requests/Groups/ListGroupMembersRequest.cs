using SeafClient.Requests.UserAccountInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    public class ListGroupMembersRequest : SessionRequest<List<AccountInfo>>
    {        
        public override string CommandUri
        {
            get
            {
                if (AvatarSize.HasValue)
                    return String.Format("api/v2.1/groups/{0:d}/members/?avatar_size={1:d}", GroupId, AvatarSize);

                return String.Format("api/v2.1/groups/{0:d}/members/", GroupId);
            }
        }
            

        public int GroupId { get; set; }

        public int? AvatarSize { get; set; }

        public override bool SupportedWithServerVersion(Version version)
        {
            return version >= new Version(5, 1, 0);
        }

        public ListGroupMembersRequest(string authToken, int groupId, int? avatarSize = null)
            : base(authToken)
        {
            GroupId = groupId;
            AvatarSize = avatarSize;
        }


    }
}

using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    public class ListGroupsRequest : SessionRequest<SeafGroupList>
    {
        public override string CommandUri => "api2/groups/";

        public ListGroupsRequest(string authToken)
            : base(authToken)
        {
        }
    }
}

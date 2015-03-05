using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    public class ListSharedLibrariesRequest : SessionRequest<List<SeafLibrary>>
    {
        public override string CommandUri
        {
            get { return "api2/shared-repos/"; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return SeafClient.HttpAccessMethod.Get; }
        }

        public ListSharedLibrariesRequest(string authToken)
            : base(authToken)
        {
            // --
        }
    }
}

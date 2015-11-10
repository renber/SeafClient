using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to list all libraries of the user owning the given authorization token
    /// </summary>
    public class ListLibrariesRequest : SessionRequest<IList<SeafLibrary>>
    {
        public override string CommandUri
        {
            get { return "api2/repos/"; }
        }

        public ListLibrariesRequest(string authToken)
            : base(authToken)
        {
            // --
        }
    }
}

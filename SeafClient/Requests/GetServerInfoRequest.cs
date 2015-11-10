using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Retrieve general information about a seafile server
    /// </summary>
    public class GetServerInfoRequest : SeafRequest<SeafServerInfo>
    {
        public override string CommandUri
        {
            get { return "api2/server-info/"; }
        }
    }

    public class SeafServerInfo
    {
        public string Version { get; set; }

        /// <summary>
        /// List of features supported by this server
        /// (e.g. seafile-basic, office-preview, file-search)
        /// </summary>
        public String[] Features { get; set; }   
    }    
}

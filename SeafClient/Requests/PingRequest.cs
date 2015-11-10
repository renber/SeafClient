using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request for the ping command which should be answered with "pong" by the seafiel server
    /// Can be used for unauthenticated and authenticated ping
    /// </summary>
    public class PingRequest : SeafRequest<bool>
    {        
        protected string AuthorizationToken { get; private set; }
        bool useAuthentication;

        public override string CommandUri
        {
            get
            {
                if (useAuthentication)
                    return "api2/auth/ping/";
                else
                    return "api2/ping/";
            }
        }

        /// <summary>
        /// Create a ping request which does not use authentication
        /// </summary>
        public PingRequest()
        {
            useAuthentication = false;
        }

        /// <summary>
        /// Create a ping request which uses the given authentication token
        /// (Can be used to check whether the token is valid)
        /// </summary>
        public PingRequest(string authorizationToken)
        {
            if (authorizationToken == null)
                throw new ArgumentNullException("authorizationToken");

            useAuthentication = true;
            AuthorizationToken = authorizationToken;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAdditionalHeaders()
        {
            foreach (var h in base.GetAdditionalHeaders())
                yield return h;

            if (useAuthentication)
                yield return new KeyValuePair<string, string>("Authorization", "Token " + AuthorizationToken);
        }

        /// <summary>
        /// Read the server response for this command
        /// </summary>        
        public override async Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return content == "\"pong\"";
        }        
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Base class for requests which need an authorization token
    /// </summary>    
    public abstract class SessionRequest<TResponse> : SeafRequest<TResponse>
    {
        /// <summary>
        /// The authorization token to use for this request
        /// </summary>
        protected string AuthorizationToken { get; private set; }

        /// <summary>
        /// Create a new request which needs an authorization token
        /// </summary>
        /// <param name="authToken">The authorization token to use for this request</param>
        public SessionRequest(string authToken)
        {
            AuthorizationToken = authToken;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetAdditionalHeaders()
        {
            foreach (var h in base.GetAdditionalHeaders())
                yield return h;

            yield return new KeyValuePair<string, string>("Authorization", "Token " + AuthorizationToken);
        }
    }
}

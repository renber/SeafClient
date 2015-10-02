using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to receive general account information of the user
    /// who owns the current session
    /// </summary>
    public class AccountInfoRequest : SessionRequest<AccountInfo>
    {
        public override string CommandUri
        {
            get { return "api2/account/info/"; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return SeafClient.HttpAccessMethod.Get; }
        }

        public override string GetErrorDescription(HttpResponseMessage msg)
        {
            switch(msg.StatusCode)
            {
                case System.Net.HttpStatusCode.Forbidden:
                    return "Invalid token";
                default:
                    return base.GetErrorDescription(msg);
            }
        }

        public AccountInfoRequest(string authToken)
            : base(authToken)
        {
            // --
        }
    }

    /// <summary>
    /// Describes the account info of a seafile account
    /// </summary>
    public class AccountInfo
    {
        public string Nickname { get; set; }
        public string Email { get; set; }

        /// <summary>
        /// The space which the user's data consumes in bytes
        /// </summary>
        public long Usage { get; set; }

        /// <summary>
        /// The quota of the user in bytes
        /// If this is -2 then the user is not limited in space
        /// </summary>
        [JsonProperty("total")]
        public long Quota { get; set; }

        /// <summary>
        /// Returns if this user has no quota
        /// </summary>
        public bool HasUnlimitedSpace
        {
            get
            {
                return Quota == -2;
            }
        }
    }
}

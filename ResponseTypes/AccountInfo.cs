using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace SeafClient.ResponseTypes
{
    /// <summary>
    /// Describes the account info of a seafile account
    /// </summary>
    public class AccountInfo
    {
        public string Nickname { get; set; }
        public string Email { get; set; }

        public long Usage { get; set; }

        /// <summary>
        /// The quota of the user
        /// If this is -2 then the user i snot limited in space
        /// </summary>
        [JsonProperty("total")]
        public long Quota { get; set; }                
    }
}

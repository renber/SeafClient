using Newtonsoft.Json;
using SeafClient.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    public class UserAvatarRequest : SessionRequest<UserAvatar>
    {
        /// <summary>
        /// The username to get the avatar for
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The size to which the avatar should be resized
        /// </summary>
        public int Size { get; set; }

        public override string CommandUri
        {
            get { return String.Format("api2/avatars/user/{0}/resized/{1:d}/", Username, Size); }
        }

        public UserAvatarRequest(string authToken, string username, int size)
            : base(authToken)
        {
            Username = username;
            Size = size;
        }
    }

    /// <summary>
    /// Describes the avatar image of a user
    /// </summary>
    public class UserAvatar
    {
        public string Url { get; set; }

        [JsonProperty("is_default")]
        public bool IsDefault { get; set; }

        [JsonProperty("mtime"), JsonConverter(typeof(SeafTimestampConverter))]
        public DateTime Timestamp { get; set; }
    }
}

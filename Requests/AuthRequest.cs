using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Requests
{
    /// <summary>
    /// Seafile command to authenticate a user
    /// </summary>
    class AuthRequest : SeafRequest<AuthResponse>
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public override string CommandUri
        {
            get { return "api2/auth-token/"; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Post; }
        }

        public AuthRequest(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            foreach (var p in base.GetPostParameters())
                yield return p;

            yield return new KeyValuePair<string, string>("username", Username);
            yield return new KeyValuePair<string, string>("password", Password);

        }
    }

    /// <summary>
    /// Response to the authenticate command
    /// </summary>
    public class AuthResponse
    {
        public string Token { get; set; }
    }
}

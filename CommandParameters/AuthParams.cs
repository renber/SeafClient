using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.CommandParameters
{
    /// <summary>
    /// Parameters for the Authenticate seafile command
    /// </summary>
    public class AuthParams : SeafCommandParams
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public AuthParams(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public override IEnumerable<KeyValuePair<string, string>> ToList()
        {
            yield return new KeyValuePair<string, string>("username", Username);
            yield return new KeyValuePair<string, string>("password", Password);
        }
    }
}

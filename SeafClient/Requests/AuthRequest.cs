using SeafClient.Types;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Seafile command to authenticate a user
    /// After the request is built, the password array is zeroed out and the 
    /// AuthRequest cannot be used again
    /// </summary>
    public class AuthRequest : SeafRequest<AuthResponse>
    {
        protected string Username { get; set; }
        protected string Password { get; set;  }

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
            return new[]
            {
                new KeyValuePair<string, string>("username", Username),
                new KeyValuePair<string, string>("password", Password),
            };
        }

        public override SeafError GetSeafError(HttpResponseMessage msg)
        {
            if (msg.StatusCode == HttpStatusCode.BadRequest)
                return new SeafError(msg.StatusCode, SeafErrorCode.InvalidCredentials);            
            else
                return base.GetSeafError(msg);
        }

        void ClearPassword()
        {
            Password = null;
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

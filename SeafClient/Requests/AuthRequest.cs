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
        protected char[] Password { get; set;  }

        public override string CommandUri
        {
            get { return "api2/auth-token/"; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Custom; }
        }

        public AuthRequest(string username, char[] password)
        {
            Username = username;
            Password = password;
        }

        public override HttpRequestMessage GetCustomizedRequest(Uri serverUri)
        {
            try
            {                                
                Uri uri = new Uri(serverUri, CommandUri);

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, uri);
                
                message.Headers.Referrer = uri;
                foreach (var hi in GetAdditionalHeaders())
                    message.Headers.Add(hi.Key, hi.Value);

                message.Content = new CredentialFormContent(new KeyValuePair<String, Char[]>("username", Username.ToCharArray()), new KeyValuePair<String, Char[]>("password", Password));

                return message;
            }
            finally
            {
                ClearPassword();
            }
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
            Array.Clear(Password, 0, Password.Length);
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

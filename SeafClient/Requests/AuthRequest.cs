using SeafClient.Types;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Seafile command to authenticate a user
    /// </summary>
    public class AuthRequest : SeafRequest<AuthResponse>
    {
        public string Username { get; set; }        
        public byte[] Password { get; set; }

        public override string CommandUri
        {
            get { return "api2/auth-token/"; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Custom; }
        }

        public AuthRequest(string username, byte[] password)
        {
            Username = username;
            Password = password;
        }

        public override HttpRequestMessage GetCustomizedRequest(string serverUri)
        {
            try
            {
                //return await client.PostAsync(uri, content);
                if (!serverUri.EndsWith("/"))
                    serverUri += "/";
                Uri uri = new Uri(serverUri + CommandUri);

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, uri);
                
                message.Headers.Referrer = uri;
                foreach (var hi in GetAdditionalHeaders())
                    message.Headers.Add(hi.Key, hi.Value);

                message.Content = new CredentialFormContent(Username, Password);

                return message;
            }
            finally
            {
                ClearPassword();
            }
        }

        void ClearPassword()
        {
            for (int i = 0; i < Password.Length; i++)
                Password[i] = 0;
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

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
    class AuthRequest : SeafRequest<AuthResponse>
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

        public override async Task<HttpResponseMessage> SendRequestCustomizedAsync(string serverUri)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    if (!serverUri.EndsWith("/"))
                        serverUri += "/";

                    Uri uri = new Uri(serverUri + CommandUri);

                    client.DefaultRequestHeaders.Referrer = uri;

                    foreach (var hi in GetAdditionalHeaders())
                        client.DefaultRequestHeaders.Add(hi.Key, hi.Value);

                    HttpContent content = new CredentialFormContent(Username, Password);
                    return await client.PostAsync(uri, content);                                        
                }
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

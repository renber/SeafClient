using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using SeafClient.Utils;
using Newtonsoft.Json;

namespace SeafClient.Requests.Libraries
{
    /// <summary>
    /// Request to create a new library in the users account
    /// </summary>
             
    public class CreateLibraryRequest : SessionRequest<CreateLibraryResponse>
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public bool CreateEncrypted { get; set; }
        public char[] Password { get; set; }

        public override string CommandUri { get { return String.Format("api2/repos/"); } }

        public override HttpAccessMethod HttpAccessMethod { get { return HttpAccessMethod.Custom; } }        

        /// <summary>
        /// Request to create a new library with the given name, description and password (if encrypted)
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="libraryName"></param>
        private CreateLibraryRequest(string authToken, string libraryName, string descriptionName, bool encrypted,  char[] password)
            : base(authToken)
        {
            Name = libraryName;
            Description = descriptionName;
            CreateEncrypted = true;
            Password = password;
        }

        /// <summary>
        /// Request to create a new unencrypted library with the given name and description
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="libraryName"></param>
        public CreateLibraryRequest(string authToken, string libraryName, string description)
            : this(authToken, libraryName, description, false, null)
        {
            // --
        }

        /// <summary>
        /// Request to create a new encrypted library with the given name, description and password
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="libraryName"></param>
        public CreateLibraryRequest(string authToken, string libraryName, string description, char[] password)
            : this(authToken, libraryName, description, true, password)
        {
            // --
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

                if (CreateEncrypted && Password != null)
                {
                    message.Content = new CredentialFormContent(new KeyValuePair<String, Char[]>("name", Name.ToCharArray()),
                                                                new KeyValuePair<String, Char[]>("desc", Description.ToCharArray()),
                                                                new KeyValuePair<String, Char[]>("password", Password));
                }
                else
                {
                    message.Content = new CredentialFormContent(new KeyValuePair<String, Char[]>("name", Name.ToCharArray()),
                                                                new KeyValuePair<String, Char[]>("desc", Description.ToCharArray()));
                }

                return message;
            }
            finally
            {
                ClearPassword();
            }
        }

        void ClearPassword()
        {
            if (Password != null)
                Array.Clear(Password, 0, Password.Length);
        }
    }

    public class CreateLibraryResponse
    {
        [JsonProperty("repo_id")]
        public virtual string Id { get; set; }

        // the actual response contains more fields
        // but we do not need them atm        
        // see https://manual.seafile.com/develop/web_api.html#create-library
    }
}

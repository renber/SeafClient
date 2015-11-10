using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SeafClient.Types;
using SeafClient.Utils;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to decrypt a library on the server
    /// (password will be stored on the server for one hour)
    /// </summary>
    public class DecryptLibraryRequest : SessionRequest<bool>
    {
        public string LibraryId { get; private set; }

        public char[] Password { get; private set; }

        public override string CommandUri
        {
            get
            {
                return String.Format("api2/repos/{0}/", LibraryId);
            }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get
            {
                return HttpAccessMethod.Custom;
            }
        }

        public DecryptLibraryRequest(string authToken, string libraryId, char[] password)
            : base(authToken)
        {
            LibraryId = libraryId;
            Password = password;
        }

        public override SeafError GetSeafError(HttpResponseMessage msg)
        {
            if (msg.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return new SeafError(msg.StatusCode, SeafErrorCode.InvalidLibraryPassword);
            if (msg.StatusCode == System.Net.HttpStatusCode.Conflict)
                return new SeafError(msg.StatusCode, SeafErrorCode.LibraryIsNotEncrypted);

            return base.GetSeafError(msg);
        }

        public async override Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string result = await msg.Content.ReadAsStringAsync();
            return result == "\"success\"";            
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

                message.Content = new CredentialFormContent(new KeyValuePair<String, Char[]>("password", Password));

                return message;
            }
            finally
            {
                ClearPassword();
            }
        }

        void ClearPassword()
        {
            Array.Clear(Password, 0, Password.Length);
        }

    }
}

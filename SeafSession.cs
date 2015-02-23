using Newtonsoft.Json;
using SeafClient.CommandParameters;
using SeafClient.ResponseTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    /// <summary>
    /// Represents an established seafile session
    /// and offers methods for data access
    /// All methods are executed asynchronously
    /// </summary>
    public class SeafSession
    {
        public string ServerUri { get; private set; }
        public string AuthToken { get; private set; }

        /// <summary>
        /// Tries to connect to the given seafile server
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> EstablishAsync(string serverUri, string username, string pwd)
        {
            // authenticate the user
            var response = await SeafWebAPI.SendCommandAsync<AuthResponse>(serverUri, SeafCommand.Authenticate, new AuthParams(username, pwd));
            return new SeafSession(serverUri, response.Token);
        } 

        private SeafSession(string serverUri, string authToken)
        {
            ServerUri = serverUri;
            AuthToken = authToken;
        }

        /// <summary>
        /// Retrieve the account info for the current session
        /// </summary>
        /// <returns></returns>
        public async Task<AccountInfo> CheckAccountInfo()
        {
            return await SeafWebAPI.SendCommandAsync<AccountInfo>(ServerUri, AuthToken, SeafCommand.CheckAccountInfo);
        }

        public async Task<List<Library>> ListLibraries()
        {
            return await SeafWebAPI.SendCommandAsync <List<Library>>(ServerUri, AuthToken, SeafCommand.ListLibraries);
        }

        public async Task<List<DirEntry>> ListDirectory(Library library, string directory)
        {
            return await SeafWebAPI.SendCommandAsync<List<DirEntry>>(ServerUri, AuthToken, SeafCommand.ListDirectoryEntries, 
                new DirectoryParams(library.ID, directory));
        }

        public async Task<string> GetFileDownloadLink(Library library, string path)
        {
            return await SeafWebAPI.SendCommandAsync<string>(ServerUri, AuthToken, SeafCommand.GetFileDownloadLink,
                new FileParams(library.ID, path));
        }
    }
}

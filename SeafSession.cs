using Newtonsoft.Json;
using SeafClient.Requests;
using SeafClient.Exceptions;
using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SeafClient
{
    /// <summary>
    /// Represents an established seafile session
    /// and offers methods for data access
    /// All methods are executed asynchronously
    /// </summary>
    public class SeafSession
    {
        /// <summary>
        /// The user this session belongs to
        /// </summary>
        public string Username { get; private set; }

        public string ServerUri { get; private set; }
        public string AuthToken { get; private set; }

        /// <summary>
        /// Tries to connect to the given seafile server
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> Establish(string serverUri, string username, string pwd)
        {
            if (!serverUri.EndsWith("/"))
                serverUri += "/";

            // authenticate the user
            AuthRequest req = new AuthRequest(username, pwd);
            var response = await SeafWebAPI.SendRequestAsync(serverUri, req);
            return new SeafSession(username, serverUri, response.Token);  
        } 

        private SeafSession(string username, string serverUri, string authToken)
        {
            Username = username;
            ServerUri = serverUri;
            AuthToken = authToken;
        }

        /// <summary>
        /// Retrieve the account info for the current session
        /// </summary>
        /// <returns></returns>
        public async Task<AccountInfo> CheckAccountInfo()
        {
            AccountInfoRequest req = new AccountInfoRequest(AuthToken);

            return await SeafWebAPI.SendRequestAsync<AccountInfo>(ServerUri, req);
        }

        /// <summary>
        /// Retrieve the avatar of the current user
        /// </summary>        
        public async Task<UserAvatar> GetUserAvatar(int size)
        {
            return await GetUserAvatar(Username, size);
        }

        /// <summary>
        /// Retrieve the avatar of the given user
        /// </summary>        
        public async Task<UserAvatar> GetUserAvatar(string username, int size)
        {
            UserAvatarRequest req = new UserAvatarRequest(AuthToken, username, size);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req);
        }

        /// <summary>
        /// List all libraries of the current user (exlcudign shared libraries from other users)
        /// </summary>
        /// <returns></returns>
        public async Task<List<SeafLibrary>> ListLibraries()
        {
            ListLibrariesRequest req = new ListLibrariesRequest(AuthToken);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req);            
        }

        /// <summary>
        /// Return all shared libraries of the current user
        /// </summary>
        /// <returns></returns>
        public async Task<List<SeafLibrary>> ListSharedLibraries()
        {
            ListSharedLibrariesRequest req = new ListSharedLibrariesRequest(AuthToken);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req);
        }

        /// <summary>
        /// List the content of the given directory of the given linrary
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public async Task<List<SeafDirEntry>> ListDirectory(SeafLibrary library, string directory)
        {
            if (!directory.EndsWith("/"))
                directory += "/";

            ListDirectoryEntriesRequest req = new ListDirectoryEntriesRequest(AuthToken, library.Id, directory);
            var dLst = await SeafWebAPI.SendRequestAsync(ServerUri, req);
            // set the path of the items              

            foreach (var d in dLst)
                d.Path = directory + d.Name;
            return dLst;
        }

        /// <summary>
        /// Create the given directory in the given library
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public async Task<bool> CreateDirectory(SeafLibrary library, string directory)
        {
            CreateDirectoryRequest req = new CreateDirectoryRequest(AuthToken, library.Id, directory);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req); 
        }

        /// <summary>
        /// Delete the given directory in the given library
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public async Task<bool> DeleteDirectory(SeafLibrary library, string directory)
        {
            return await DeleteFile(library, directory);   
        }

        /// <summary>
        /// Delete the given directory in the given library
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public async Task<bool> DeleteFile(SeafLibrary library, string filePath)
        {
            DeleteDirEntryRequest req = new DeleteDirEntryRequest(AuthToken, library.Id, filePath);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req); 
        }

        /// <summary>
        /// Get a download link for the given file
        /// </summary>
        /// <param name="library"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<string> GetFileDownloadLink(SeafLibrary library, string path)
        {
            GetFileDownloadLinkRequest req = new GetFileDownloadLinkRequest(AuthToken, library.Id, path);
            return await SeafWebAPI.SendRequestAsync(ServerUri, req);             
        }

        /// <summary>
        /// Uploads a single file
        /// </summary>
        /// <param name="uploadLink"></param>
        /// <param name="targetFilename"></param>
        /// <param name="fileContent"></param>
        /// <returns></returns>
        public async Task<bool> UploadSingle(SeafLibrary library, string targetDirectory, string targetFilename, Stream fileContent, Action<float> progressCallback)
        {
            // to upload files we need to get a uplaod link first
            GetUploadLinkRequest req = new GetUploadLinkRequest(AuthToken, library.Id);
            string uploadLink = await SeafWebAPI.SendRequestAsync(ServerUri, req);

            UploadRequest upReq = new UploadRequest(AuthToken, uploadLink, targetDirectory, targetFilename, fileContent, progressCallback);
            return await SeafWebAPI.SendRequestAsync(ServerUri, upReq);
        }
        
    }
}

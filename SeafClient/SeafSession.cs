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

        private ISeafWebConnection webConnection;

        /// <summary>
        /// Tries to connect to the given seafile server using the default ISeafWebConnection implementation and returns an appropriate session object on success
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> Establish(string serverUri, string username, byte[] pwd)
        {
            return await Establish(new SeafHttpConnection(), serverUri, username, pwd);
        }

        /// <summary>
        /// Tries to connect to the given seafile server using the given ISeafWebConnection implementation and returns an appropriate session object on success
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> Establish(ISeafWebConnection seafWebConnection, string serverUri, string username, byte[] pwd)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException("seafWebConnection");
            if (serverUri == null)
                throw new ArgumentNullException("serverUri");
            if (username == null)
                throw new ArgumentNullException("username");
            if (pwd == null)
                throw new ArgumentNullException("pwd");

            if (!serverUri.EndsWith("/"))
                serverUri += "/";

            // authenticate the user
            AuthRequest req = new AuthRequest(username, pwd);
            var response = await seafWebConnection.SendRequestAsync(serverUri, req);
            return new SeafSession(seafWebConnection, username, serverUri, response.Token);  
        } 

        /// <summary>
        /// Wraps an already existing seafile session
        /// use SeafSession.Establish(...) to establish a new connection and retrieve an authentication token
        /// from the Seafile server
        /// </summary>
        /// <param name="username">The username of the account authToken belongs to</param>
        /// <param name="serverUri">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="authToken">The authentication token as received from the Seafile server</param>
        private SeafSession(ISeafWebConnection seafWebConnection, string username, string serverUri, string authToken)
        {
            webConnection = seafWebConnection;
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

            return await webConnection.SendRequestAsync<AccountInfo>(ServerUri, req);
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
            return await webConnection.SendRequestAsync(ServerUri, req);
        }

        /// <summary>
        /// List all libraries of the current user (excluding shared libraries from other users)
        /// </summary>
        /// <returns></returns>
        public async Task<IList<SeafLibrary>> ListLibraries()
        {
            ListLibrariesRequest req = new ListLibrariesRequest(AuthToken);
            return await webConnection.SendRequestAsync(ServerUri, req);            
        }

        /// <summary>
        /// Return all shared libraries of the current user
        /// </summary>
        /// <returns></returns>
        public async Task<IList<SeafSharedLibrary>> ListSharedLibraries()
        {
            ListSharedLibrariesRequest req = new ListSharedLibrariesRequest(AuthToken);
            return await webConnection.SendRequestAsync(ServerUri, req);
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
            var dLst = await webConnection.SendRequestAsync(ServerUri, req);
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
            return await webConnection.SendRequestAsync(ServerUri, req); 
        }

        /// <summary>
        /// Rename the given directory
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directoryPath"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public async Task<bool> RenameDirectory(SeafLibrary library, string directoryPath, string newName)
        {
            RenameDirectoryRequest req = new RenameDirectoryRequest(AuthToken, library.Id, directoryPath, newName);
            return await webConnection.SendRequestAsync(ServerUri, req);
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
        /// Rename the given file
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directoryPath"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public async Task<bool> RenameFile(SeafLibrary library, string filePath, string newName)
        {
            RenameFileRequest req = new RenameFileRequest(AuthToken, library.Id, filePath, newName);
            return await webConnection.SendRequestAsync(ServerUri, req);
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
            return await webConnection.SendRequestAsync(ServerUri, req); 
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
            return await webConnection.SendRequestAsync(ServerUri, req);             
        }

        /// <summary>
        /// Get a thumbnail for the given image
        /// </summary>
        /// <param name="library"></param>
        /// <param name="path"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public async Task<byte[]> GetThumbnailImage(SeafLibrary library, string path, int size)
        {
            GetThumbnailImageRequest req = new GetThumbnailImageRequest(AuthToken, library.Id, path, size);
            return await webConnection.SendRequestAsync(ServerUri, req);
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
            string uploadLink = await webConnection.SendRequestAsync(ServerUri, req);

            UploadRequest upReq = new UploadRequest(AuthToken, uploadLink, targetDirectory, targetFilename, fileContent, progressCallback);
            return await webConnection.SendRequestAsync(ServerUri, upReq);
        }
        
    }
}

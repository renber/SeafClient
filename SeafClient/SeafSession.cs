using Newtonsoft.Json;
using SeafClient.Requests;
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

        public Uri ServerUri { get; private set; }
        public string AuthToken { get; private set; }

        private ISeafWebConnection webConnection;

        /// <summary>
        /// Tries to connect to the given seafile server using the default ISeafWebConnection implementation and returns an appropriate session object on success
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> Establish(Uri serverUri, string username, string pwd)
        {
            return await Establish(SeafConnectionFactory.GetDefaultConnection(), serverUri, username, pwd);
        }

        /// <summary>
        /// Tries to connect to the given seafile server using the given ISeafWebConnection implementation and returns an appropriate session object on success
        /// </summary>
        /// <param name="serverUrl">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">The password for the given user</param>
        public static async Task<SeafSession> Establish(ISeafWebConnection seafWebConnection, Uri serverUri, string username, string pwd)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException("seafWebConnection");
            if (serverUri == null)
                throw new ArgumentNullException("serverUri");
            if (username == null)
                throw new ArgumentNullException("username");
            if (pwd == null)
                throw new ArgumentNullException("pwd");            

            // authenticate the user
            AuthRequest req = new AuthRequest(username, pwd);
            var response = await seafWebConnection.SendRequestAsync(serverUri, req);
            return new SeafSession(seafWebConnection, username, serverUri, response.Token);  
        }

        /// <summary>
        /// Create a seafile session for the given authentication token
        /// Will automatically connect to the seafile server and check if the token is valid
        /// and retrieve the username for the given token
        /// </summary>
        public static async Task<SeafSession> FromToken(Uri serverUri, string authToken)
        {
            return await FromToken(SeafConnectionFactory.GetDefaultConnection(), serverUri, authToken);
        }

        /// <summary>
        /// Create a seafile session for the given authentication token
        /// Will automatically connect to the seafile server and check if the token is valid
        /// and retrieve the username for the given token using the given ISeafWebConnection
        /// </summary>
        public static async Task<SeafSession> FromToken(ISeafWebConnection seafWebConnection, Uri serverUri, string authToken)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException("seafWebConnection");
            if (serverUri == null)
                throw new ArgumentNullException("serverUri");
            if (authToken == null)
                throw new ArgumentNullException("authToken");

            // get the user for the token and check if the token is valid at the same time
            AccountInfoRequest infoReq = new AccountInfoRequest(authToken);
            var accInfo = await seafWebConnection.SendRequestAsync(serverUri, infoReq);
            return new SeafSession(seafWebConnection, accInfo.Email, serverUri, authToken);
        }

        /// <summary>
        /// Create a seafile session for the given username and authentication token 
        /// The validity of the username or token are not checked
        /// (if they are wrong you may not be able to execute requests)
        /// </summary>
        public static SeafSession FromToken(Uri serverUri, string username, string authToken)
        {
            return FromToken(SeafConnectionFactory.GetDefaultConnection(), serverUri, username, authToken);
        }

        /// <summary>
        /// Create a seafile session for the given username and authentication token using the given ISeafWebConnection
        /// The validity of the username or token are not checked
        /// (if they are wrong you may not be able to execute requests)
        /// </summary>
        public static SeafSession FromToken(ISeafWebConnection seafWebConnection, Uri serverUri, string username, string authToken)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException("seafWebConnection");
            if (serverUri == null)
                throw new ArgumentNullException("serverUri");
            if (username == null)
                throw new ArgumentNullException("username");
            if (authToken == null)
                throw new ArgumentNullException("authToken");
            
            return new SeafSession(seafWebConnection, username, serverUri, authToken);
        }

        /// <summary>
        /// Wraps an already existing seafile session
        /// use SeafSession.Establish(...) to establish a new connection and retrieve an authentication token
        /// from the Seafile server
        /// </summary>
        /// <param name="username">The username of the account authToken belongs to</param>
        /// <param name="serverUri">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="authToken">The authentication token as received from the Seafile server</param>
        private SeafSession(ISeafWebConnection seafWebConnection, string username, Uri serverUri, string authToken)
        {
            webConnection = seafWebConnection;
            Username = username;
            ServerUri = serverUri;
            AuthToken = authToken;
        }

        /// <summary>
        /// Ping the server without authentication
        /// </summary>
        /// <param name="serverUri"></param>
        /// <returns></returns>
        public static async Task<bool> Ping(Uri serverUri)
        {
            return await Ping(SeafConnectionFactory.GetDefaultConnection(), serverUri);
        }

        /// <summary>
        /// Ping the server without authentication using the given ISeafWebConnection
        /// </summary>
        /// <param name="serverUri"></param>
        /// <returns></returns>
        public static async Task<bool> Ping(ISeafWebConnection seafWebConnection, Uri serverUri)
        {
            PingRequest r = new PingRequest();
            return await seafWebConnection.SendRequestAsync(serverUri, r);
        }

        /// <summary>
        /// Retrieve some general information about the Seafile server at the given address
        /// </summary>
        /// <param name="seafWebConnection"></param>
        /// <param name="serverUri"></param>
        /// <returns></returns>
        public static async Task<SeafServerInfo> GetServerInfo(Uri serverUri)
        {
            return await GetServerInfo(SeafConnectionFactory.GetDefaultConnection(), serverUri);
        }

        /// <summary>
        /// Retrieve some general information about the Seafile server at the given address using
        /// the given ISeafWebConnection
        /// </summary>
        /// <param name="seafWebConnection"></param>
        /// <param name="serverUri"></param>
        /// <returns></returns>
        public static async Task<SeafServerInfo> GetServerInfo(ISeafWebConnection seafWebConnection, Uri serverUri)
        {
            GetServerInfoRequest r = new GetServerInfoRequest();
            return await seafWebConnection.SendRequestAsync(serverUri, r);
        }

        /// <summary>
        /// Ping the server using the current session
        /// (Can be used to check whether the session is still valid)
        /// </summary>        
        /// <returns></returns>
        public async Task<bool> Ping()
        {
            PingRequest r = new PingRequest(AuthToken);
            return await webConnection.SendRequestAsync(ServerUri, r);
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
        /// List the content of the root directory ("/") of the given linrary
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListDirectory(SeafLibrary library)
        {
            return await ListDirectory(library, "/");
        }

        /// <summary>
        /// List the content of the given directory of the given linrary
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public async Task<IList<SeafDirEntry>> ListDirectory(SeafLibrary library, string directory)
        {
            if (!directory.EndsWith("/"))
                directory += "/";

            ListDirectoryEntriesRequest req = new ListDirectoryEntriesRequest(AuthToken, library.Id, directory);
            var dLst = await webConnection.SendRequestAsync(ServerUri, req);            
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
        /// Move the given file
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directoryPath"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public async Task<bool> CopyFile(SeafLibrary library, string filePath, SeafLibrary targetLibrary, string targetDirectory)
        {
            CopyFileRequest req = new CopyFileRequest(AuthToken, library.Id, filePath, targetLibrary.Id, targetDirectory);
            return await webConnection.SendRequestAsync(ServerUri, req);
        }

        /// <summary>
        /// Move the given file
        /// </summary>
        /// <param name="library"></param>
        /// <param name="directoryPath"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public async Task<bool> MoveFile(SeafLibrary library, string filePath, SeafLibrary targetLibrary, string targetDirectory)
        {
            MoveFileRequest req = new MoveFileRequest(AuthToken, library.Id, filePath, targetLibrary.Id, targetDirectory);
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

        /// <summary>
        /// Provide the password for the given encrypted library
        /// in order to be able to access its contents
        /// </summary>
        public async Task<bool> DecryptLibrary(SeafLibrary library, char[] password)
        {
            DecryptLibraryRequest r = new DecryptLibraryRequest(AuthToken, library.Id, password);
            return await webConnection.SendRequestAsync(ServerUri, r);
        }
        
    }
}

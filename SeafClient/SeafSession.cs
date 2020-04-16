using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SeafClient.Requests;
using SeafClient.Requests.Directories;
using SeafClient.Requests.Files;
using SeafClient.Requests.Groups;
using SeafClient.Requests.Libraries;
using SeafClient.Requests.StarredFiles;
using SeafClient.Requests.UserAccountInfo;
using SeafClient.Types;
using SeafClient.Utils;

namespace SeafClient
{
    /// <summary>
    ///     Represents an established seafile session
    ///     and offers methods for data access
    ///     All methods are executed asynchronously
    /// </summary>
    public class SeafSession
    {
        private readonly ISeafWebConnection _webConnection;


        /// <summary>
        /// The user this session belongs to
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// The Uri of the seafile server
        /// </summary>
        public Uri ServerUri { get; }

        /// <summary>
        /// The session's authentication token
        /// </summary>
        public string AuthToken { get; }

        /// <summary>
        /// The version of the server this session is connected to
        /// </summary>
        public Version ServerVersion { get; private set; }

        /// <summary>
        ///     Wraps an already existing seafile session
        ///     use SeafSession.Establish(...) to establish a new connection and retrieve an authentication token
        ///     from the Seafile server
        /// </summary>
        /// <param name="seafWebConnection">The seaf web connection</param>
        /// <param name="username">The username of the account authToken belongs to</param>
        /// <param name="serverUri">The server url to connect to (including protocol (http or https) and port)</param>
        /// <param name="authToken">The authentication token as received from the Seafile server</param>
        private SeafSession(ISeafWebConnection seafWebConnection, string username, Uri serverUri, string authToken, Version serverVersion)
        {
            _webConnection = seafWebConnection;
            Username = username;
            ServerUri = serverUri;
            AuthToken = authToken;
            ServerVersion = serverVersion;
        }

        /// <summary>
        ///     Tries to connect to the given seafile server using the default ISeafWebConnection implementation and returns an
        ///     appropriate session object on success
        /// </summary>
        /// <param name="serverUri">The server uri to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">
        ///     The password for the given user (will be overwritten with zeros as soon as the authentication request
        ///     has been sent)
        /// </param>
        public static async Task<SeafSession> Establish(Uri serverUri, string username, char[] pwd)
        {
            return await Establish(SeafConnectionFactory.GetDefaultConnection(), serverUri, username, pwd);
        }

        /// <summary>
        ///     Tries to connect to the given seafile server using the given ISeafWebConnection implementation and returns an
        ///     appropriate session object on success
        /// </summary>
        /// <param name="seafWebConnection">The seaf web connection</param>
        /// <param name="serverUri">The server uri to connect to (including protocol (http or https) and port)</param>
        /// <param name="username">The username to login with</param>
        /// <param name="pwd">
        ///     The password for the given user (will be overwritten with zeros as soon as the authentication request
        ///     has been sent)
        /// </param>
        public static async Task<SeafSession> Establish(ISeafWebConnection seafWebConnection, Uri serverUri, string username, char[] pwd)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException(nameof(seafWebConnection));
            if (serverUri == null)
                throw new ArgumentNullException(nameof(serverUri));
            if (username == null)
                throw new ArgumentNullException(nameof(username));
            if (pwd == null)
                throw new ArgumentNullException(nameof(pwd));

            // authenticate the user
            var request = new AuthRequest(username, pwd);
            var response = await seafWebConnection.SendRequestAsync(serverUri, request);

            // get the server version
            Version v = new Version(0, 0, 0);
            try
            {
                var verResponse = await GetServerInfo(seafWebConnection, serverUri);
                if (!Version.TryParse(verResponse.Version, out v))
                    v = new Version(0, 0, 0);
            }
            catch (Exception e)
            {
                v = new Version(0, 0, 0);
            }

            return new SeafSession(seafWebConnection, username, serverUri, response.Token, v);
        }

        /// <summary>
        ///     Create a seafile session for the given authentication token
        ///     Will automatically connect to the seafile server and check if the token is valid
        ///     and retrieve the username for the given token
        /// </summary>
        public static async Task<SeafSession> FromToken(Uri serverUri, string authToken)
        {
            return await FromToken(SeafConnectionFactory.GetDefaultConnection(), serverUri, authToken);
        }

        /// <summary>
        ///     Create a seafile session for the given authentication token
        ///     Will automatically connect to the seafile server and check if the token is valid
        ///     and retrieve the username for the given token using the given ISeafWebConnection
        /// </summary>
        public static async Task<SeafSession> FromToken(ISeafWebConnection seafWebConnection, Uri serverUri, string authToken)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException(nameof(seafWebConnection));
            if (serverUri == null)
                throw new ArgumentNullException(nameof(serverUri));
            if (authToken == null)
                throw new ArgumentNullException(nameof(authToken));

            // get the user for the token and check if the token is valid at the same time
            var infoRequest = new AccountInfoRequest(authToken);
            var accInfo = await seafWebConnection.SendRequestAsync(serverUri, infoRequest);

            // get the server version
            Version v = new Version(0, 0, 0);
            try
            {                
                var verResponse = await GetServerInfo(seafWebConnection, serverUri);
                if (!Version.TryParse(verResponse.Version, out v))
                    v = new Version(0, 0, 0);
            }
            catch (Exception e)
            {
                v = new Version(0, 0, 0);
            }

            return new SeafSession(seafWebConnection, accInfo.Email, serverUri, authToken, v);
        }

        /// <summary>
        ///     Create a seafile session for the given username and authentication token
        ///     The validity of the username, token or server version are not checked
        ///     (if they are wrong you may not be able to execute requests)
        /// </summary>
        public static SeafSession FromToken(Uri serverUri, string username, string authToken, Version serverVersion)
        {
            return FromToken(SeafConnectionFactory.GetDefaultConnection(), serverUri, username, authToken, serverVersion);
        }

        /// <summary>
        ///     Create a seafile session for the given username and authentication token using the given ISeafWebConnection
        ///     The validity of the username, token or server version are not checked
        ///     (if they are wrong you may not be able to execute requests)
        /// </summary>
        public static SeafSession FromToken(ISeafWebConnection seafWebConnection, Uri serverUri, string username, string authToken, Version serverVersion)
        {
            if (seafWebConnection == null)
                throw new ArgumentNullException(nameof(seafWebConnection));
            if (serverUri == null)
                throw new ArgumentNullException(nameof(serverUri));
            if (username == null)
                throw new ArgumentNullException(nameof(username));
            if (authToken == null)
                throw new ArgumentNullException(nameof(authToken));

            return new SeafSession(seafWebConnection, username, serverUri, authToken, serverVersion);
        }

        /// <summary>
        ///     Ping the server without authentication
        /// </summary>
        /// <param name="serverUri">The server uri to ping</param>
        /// <returns></returns>
        public static async Task<bool> Ping(Uri serverUri)
        {
            return await Ping(SeafConnectionFactory.GetDefaultConnection(), serverUri);
        }

        /// <summary>
        ///     Ping the server without authentication using the given ISeafWebConnection
        /// </summary>
        /// <param name="seafWebConnection">The seaf web connection</param>
        /// <param name="serverUri">The server uri to ping</param>
        /// <returns></returns>
        public static async Task<bool> Ping(ISeafWebConnection seafWebConnection, Uri serverUri)
        {
            var request = new PingRequest();
            return await seafWebConnection.SendRequestAsync(serverUri, request);
        }

        /// <summary>
        ///     Retrieve some general information about the Seafile server at the given address
        /// </summary>
        /// <param name="serverUri">The server uri to get the info</param>
        /// <returns></returns>
        public static async Task<SeafServerInfo> GetServerInfo(Uri serverUri)
        {
            return await GetServerInfo(SeafConnectionFactory.GetDefaultConnection(), serverUri);
        }

        /// <summary>
        ///     Retrieve some general information about the Seafile server at the given address using
        ///     the given ISeafWebConnection
        /// </summary>
        /// <param name="seafWebConnection">The seaf web connection</param>
        /// <param name="serverUri">The server uri to get the info</param>
        /// <returns></returns>
        public static async Task<SeafServerInfo> GetServerInfo(ISeafWebConnection seafWebConnection, Uri serverUri)
        {
            var request = new GetServerInfoRequest();
            return await seafWebConnection.SendRequestAsync(serverUri, request);
        }

        /// <summary>
        /// Send the given (custom) request to the Seafile server using the current session data
        /// </summary>
        /// <typeparam name="TResponse">The respons etype of the request</typeparam>
        /// <param name="request">The request to send</param>
        /// <param name="timeout">The request timeout (if any)</param>
        /// <returns></returns>
        public async Task<TResponse> SendRequest<TResponse>(SeafRequest<TResponse> request, TimeSpan? timeout = null)
        {
            return await _webConnection.SendRequestAsync(ServerUri, request, timeout);
        }

        /// <summary>
        ///     Ping the server using the current session
        ///     (Can be used to check whether the session is still valid)
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Ping()
        {
            var request = new PingRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Retrieve the account info for the current session
        /// </summary>
        /// <returns></returns>
        public async Task<AccountInfo> CheckAccountInfo()
        {
            var req = new AccountInfoRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, req);
        }

        /// <summary>
        ///     Retrieve the avatar of the current user
        /// </summary>
        /// <param name="size">Size of the requested image in pixels (width=height)</param>
        public async Task<UserAvatar> GetUserAvatar(int size)
        {
            return await GetUserAvatar(Username, size);
        }

        /// <summary>
        ///     Retrieve the avatar of the given user
        /// </summary>
        /// <param name="username">The username to retrieve the avatar for</param>
        /// <param name="size">Size of the requested image in pixels (width=height)</param>
        public async Task<UserAvatar> GetUserAvatar(string username, int size)
        {
            var request = new UserAvatarRequest(AuthToken, username, size);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Return the current user's default library
        /// </summary>
        /// <returns>The user's default library or null if no default library exists</returns>
        public async Task<SeafLibrary> GetDefaultLibrary()
        {
            var request = new GetDefaultLibraryRequest(AuthToken);
            var result = await _webConnection.SendRequestAsync(ServerUri, request);

            if (!result.Exists)
                return null;

            return await GetLibraryInfo(result.LibraryId);
        }

        /// <summary>
        ///     Retrieve information for the library with the given id
        /// </summary>
        public async Task<SeafLibrary> GetLibraryInfo(string libraryId)
        {
            var request = new GetLibraryInfoRequest(AuthToken, libraryId);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     List all libraries of the current user (excluding shared libraries from other users)
        /// </summary>
        /// <returns></returns>
        public async Task<IList<SeafLibrary>> ListLibraries()
        {
            var request = new ListLibrariesRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Return all shared libraries of the current user
        /// </summary>
        public async Task<IList<SeafSharedLibrary>> ListSharedLibraries()
        {
            var request = new ListSharedLibrariesRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Creates a new unencrypted library with the given name and description
        /// </summary>
        /// <returns></returns>
        public async Task<SeafLibrary> CreateLibrary(string name, string description = "")
        {
            var request = new CreateLibraryRequest(AuthToken, name, description);
            var result = await _webConnection.SendRequestAsync(ServerUri, request);
            return await GetLibraryInfo(result.Id);
        }

        /// <summary>
        ///     Creates a new encrypted library with the given name, description and password
        /// </summary>
        /// <param name="name">The name of the library</param>
        /// <param name="description">The description of the library</param>
        /// <param name="password">
        ///     The password to encrypt the library with. Will be overwritten with zeroes as soon as the request
        ///     has been sent
        /// </param>
        /// <returns></returns>
        public async Task<SeafLibrary> CreateEncryptedLibrary(string name, string description, char[] password)
        {
            var request = new CreateLibraryRequest(AuthToken, name, description, password);
            var result = await _webConnection.SendRequestAsync(ServerUri, request);
            return await GetLibraryInfo(result.Id);
        }

        /// <summary>
        ///     List the content of the root directory ("/") of the given linrary
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListDirectory(SeafLibrary library)
        {
            return await ListDirectory(library, "/");
        }

        /// <summary>
        ///     List the content of the given directory
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListDirectory(SeafDirEntry dirEntry)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.Dir)
                throw new ArgumentException("The given directory entry is not a directory.");

            return await ListDirectory(dirEntry.LibraryId, dirEntry.Path);
        }

        /// <summary>
        ///     List the content of the given directory of the given library
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListDirectory(SeafLibrary library, string directory)
        {
            library.ThrowOnNull(nameof(library));

            return await ListDirectory(library.Id, directory);
        }

        /// <summary>
        ///     List the content of the given directory of the given library
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListDirectory(string libraryId, string directory)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            directory.ThrowOnNull(nameof(directory));

            if (!directory.EndsWith("/"))
                directory += "/";

            var request = new ListDirectoryEntriesRequest(AuthToken, libraryId, directory);
            var entries = await _webConnection.SendRequestAsync(ServerUri, request);
            return entries;
        }

        /// <summary>
        ///     Create a new directory in the given library
        /// </summary>
        /// <param name="library">Library to create the directory in</param>
        /// <param name="path">Path of the directory to create</param>
        /// <returns>A value which indicates if the creation was successful</returns>
        public async Task<bool> CreateDirectory(SeafLibrary library, string path)
        {
            library.ThrowOnNull(nameof(library));

            return await CreateDirectory(library.Id, path);
        }

        /// <summary>
        ///     Create a new directory in the given library
        /// </summary>
        /// <param name="libraryId">The id of the library to create the directory in</param>
        /// <param name="path">Path of the directory to create</param>
        /// <returns>A value which indicates if the creation was successful</returns>
        public async Task<bool> CreateDirectory(string libraryId, string path)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            path.ThrowOnNull(nameof(path));

            var request = new CreateDirectoryRequest(AuthToken, libraryId, path);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Rename the given directory
        /// </summary>
        /// <param name="dirEntry">The directory entry of the directory to rename</param>
        /// <param name="newName">The new name of the directory</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameDirectory(SeafDirEntry dirEntry, string newName)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.Dir)
                throw new ArgumentException("The given directory entry is not a directory.");

            return await RenameDirectory(dirEntry.LibraryId, dirEntry.Path, newName);
        }

        /// <summary>
        ///     Rename the given directory
        /// </summary>
        /// <param name="library">The library the directory is in</param>
        /// <param name="directoryPath">The current path of the directory</param>
        /// <param name="newName">The new name of the directory</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameDirectory(SeafLibrary library, string directoryPath, string newName)
        {
            library.ThrowOnNull(nameof(library));

            return await RenameDirectory(library.Id, directoryPath, newName);
        }

        /// <summary>
        ///     Rename the given directory
        /// </summary>
        /// <param name="libraryId">The Id of the library the directory is in</param>
        /// <param name="directoryPath">The current path of the directory</param>
        /// <param name="newName">The new name of the directory</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameDirectory(string libraryId, string directoryPath, string newName)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            directoryPath.ThrowOnNull(nameof(directoryPath));
            newName.ThrowOnNull(nameof(newName));

            var request = new RenameDirectoryRequest(AuthToken, libraryId, directoryPath, newName);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Delete the given directory
        /// </summary>
        /// <param name="dirEntry">The directory to delete</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> DeleteDirectory(SeafDirEntry dirEntry)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.Dir)
                throw new ArgumentException("The given directory entry is not a directory.");

            return await DeleteDirectory(dirEntry.LibraryId, dirEntry.Path);
        }

        /// <summary>
        ///     Delete the given directory in the given library
        /// </summary>
        /// <param name="library">The library the directory is in</param>
        /// <param name="directoryPath">The path of the directory to delete</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> DeleteDirectory(SeafLibrary library, string directoryPath)
        {
            library.ThrowOnNull(nameof(library));

            return await DeleteDirectory(library.Id, directoryPath);
        }

        /// <summary>
        ///     Delete the given directory in the given library
        /// </summary>
        /// <param name="libraryId">The id of the library the directory is in</param>
        /// <param name="directoryPath">The path of the directory to delete</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> DeleteDirectory(string libraryId, string directoryPath)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            directoryPath.ThrowOnNull(nameof(directoryPath));

            return await DeleteFile(libraryId, directoryPath);
        }

        /// <summary>
        ///     Retrieve information about the given file in the given library
        /// </summary>
        /// <param name="library">The library the file belongs to</param>
        /// <param name="filePath">Path to the file</param>
        /// <returns>The directory entry of the file</returns>
        public async Task<SeafDirEntry> GetFileDetail(SeafLibrary library, string filePath)
        {
            library.ThrowOnNull(nameof(library));

            return await GetFileDetail(library.Id, filePath);
        }

        /// <summary>
        ///     Retrieve information about the given file in the given library
        /// </summary>
        /// <param name="libraryId">The id of the library the file belongs to</param>
        /// <param name="filePath">Path to the file</param>
        /// <returns>The directory entry of the file</returns>
        public async Task<SeafDirEntry> GetFileDetail(string libraryId, string filePath)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            filePath.ThrowOnNull(nameof(filePath));

            var request = new GetFileDetailRequest(AuthToken, libraryId, filePath);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Rename the given file
        /// </summary>
        /// <param name="dirEntry">The directory entry of the file</param>
        /// <param name="newName">The new name of the file</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameFile(SeafDirEntry dirEntry, string newName)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given directory entry is not a file.");

            return await RenameFile(dirEntry.LibraryId, dirEntry.Path, newName);
        }

        /// <summary>
        ///     Rename the given file
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="newName">The new name of the file</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameFile(SeafLibrary library, string filePath, string newName)
        {
            library.ThrowOnNull(nameof(library));

            return await RenameFile(library.Id, filePath, newName);
        }

        /// <summary>
        ///     Rename the given file
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="newName">The new name of the file</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> RenameFile(string libraryId, string filePath, string newName)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            filePath.ThrowOnNull(nameof(filePath));
            newName.ThrowOnNull(nameof(newName));

            var request = new RenameFileRequest(AuthToken, libraryId, filePath, newName);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Move the given file
        /// </summary>
        /// <param name="dirEntry">The file to move</param>
        /// <param name="targetLibrary">The library to move this file to</param>
        /// <param name="targetDirectory">The directory to move this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> CopyFile(SeafDirEntry dirEntry, SeafLibrary targetLibrary, string targetDirectory)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given directory entry is not a file.");

            return await CopyFile(dirEntry.LibraryId, dirEntry.Path, targetLibrary.Id, targetDirectory);
        }

        /// <summary>
        ///     Copy the given file
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="targetLibrary">The library to copy this file to</param>
        /// <param name="targetDirectory">The directory to copy this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> CopyFile(SeafLibrary library, string filePath, SeafLibrary targetLibrary, string targetDirectory)
        {
            library.ThrowOnNull(nameof(library));
            targetLibrary.ThrowOnNull(nameof(targetLibrary));

            return await CopyFile(library.Id, filePath, targetLibrary.Id, targetDirectory);
        }

        /// <summary>
        ///     Copy the given file
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="targetLibraryId">The id of the library to copy this file to</param>
        /// <param name="targetDirectory">The directory to copy this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> CopyFile(string libraryId, string filePath, string targetLibraryId,
            string targetDirectory)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            filePath.ThrowOnNull(nameof(filePath));
            targetLibraryId.ThrowOnNull(nameof(targetLibraryId));
            targetDirectory.ThrowOnNull(nameof(targetDirectory));

            var request = new CopyFileRequest(AuthToken, libraryId, filePath, targetLibraryId, targetDirectory);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Move the given file
        /// </summary>
        /// <param name="dirEntry">The directory entry of the file to move</param>
        /// <param name="targetLibrary">The library to move this file to</param>
        /// <param name="targetDirectory">The directory to move this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> MoveFile(SeafDirEntry dirEntry, SeafLibrary targetLibrary, string targetDirectory)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given directory entry is not a file.");

            return await MoveFile(dirEntry.LibraryId, dirEntry.Path, targetLibrary.Id, targetDirectory);
        }

        /// <summary>
        ///     Move the given file
        /// </summary>
        /// <param name="library">The library th file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="targetLibrary">The library to move this file to</param>
        /// <param name="targetDirectory">The directory to move this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> MoveFile(SeafLibrary library, string filePath, SeafLibrary targetLibrary, string targetDirectory)
        {
            library.ThrowOnNull(nameof(library));
            targetLibrary.ThrowOnNull(nameof(targetLibrary));

            return await MoveFile(library.Id, filePath, targetLibrary.Id, targetDirectory);
        }

        /// <summary>
        ///     Move the given file
        /// </summary>
        /// <param name="libraryId">The id of the library th file is in</param>
        /// <param name="filePath">The full path of the file</param>
        /// <param name="targetLibraryId">The id of the library to move this file to</param>
        /// <param name="targetDirectory">The directory to move this file to</param>
        /// <returns>A value which indicates if the action was successful</returns>
        public async Task<bool> MoveFile(string libraryId, string filePath, string targetLibraryId, string targetDirectory)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            filePath.ThrowOnNull(nameof(filePath));
            targetLibraryId.ThrowOnNull(nameof(targetLibraryId));
            targetDirectory.ThrowOnNull(nameof(targetDirectory));

            var request = new MoveFileRequest(AuthToken, libraryId, filePath, targetLibraryId, targetDirectory);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Delete the given file
        /// </summary>
        /// <param name="dirEntry">Directory entry of the file to delete</param>
        /// <returns>A value which indicates if the deletion was successful</returns>
        public async Task<bool> DeleteFile(SeafDirEntry dirEntry)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            return await DeleteFile(dirEntry.LibraryId, dirEntry.Path);
        }

        /// <summary>
        ///     Delete the given file in the given library
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="filePath">Path of the file</param>
        /// <returns>A value which indicates if the deletion was successful</returns>
        public async Task<bool> DeleteFile(SeafLibrary library, string filePath)
        {
            library.ThrowOnNull(nameof(library));

            return await DeleteFile(library.Id, filePath);
        }

        /// <summary>
        ///     Delete the given file in the given library
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="filePath">Path of the file</param>
        /// <returns>A value which indicates if the deletion was successful</returns>
        protected async Task<bool> DeleteFile(string libraryId, string filePath)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            filePath.ThrowOnNull(nameof(filePath));

            var request = new DeleteFileEntryRequest(AuthToken, libraryId, filePath);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Get a download link for the given file
        /// </summary>
        /// <param name="dirEntry">The directory entry for the file to download</param>
        /// <returns>The download link which is valid once</returns>
        public async Task<string> GetFileDownloadLink(SeafDirEntry dirEntry)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given directory entry is not a file.");

            return await GetFileDownloadLink(dirEntry.LibraryId, dirEntry.Path);
        }

        /// <summary>
        ///     Get a download link for the given file
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="path">The path to the file</param>
        /// <returns>The download link which is valid once</returns>
        public async Task<string> GetFileDownloadLink(SeafLibrary library, string path)
        {
            library.ThrowOnNull(nameof(library));

            return await GetFileDownloadLink(library.Id, path);
        }

        /// <summary>
        ///     Get a download link for the given file
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="path">The path to the file</param>
        /// <returns>The download link which is valid once</returns>
        protected async Task<string> GetFileDownloadLink(string libraryId, string path)
        {
	        libraryId.ThrowOnNull(nameof(libraryId));
	        path.ThrowOnNull(nameof(path));
            return await GetFileDownloadLink(libraryId, path,false);
        }

        /// <summary>
        ///     Get a download link for the given file
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="path">The path to the file</param>
        /// <param name="reuseableLink">(optional): Set reuse to true if you want the generated download link can be accessed more than once in one hour.</param>
        /// <returns>The download link which is valid once</returns>
        public async Task<string> GetFileDownloadLink(string libraryId, string path, bool reuseableLink)
        {
	        libraryId.ThrowOnNull(nameof(libraryId));
	        path.ThrowOnNull(nameof(path));

	        var request = new GetFileDownloadLinkRequest(AuthToken, libraryId, path, reuseableLink);
	        return await _webConnection.SendRequestAsync(ServerUri, request);
        }


        /// <summary>
        ///     Get a thumbnail for the given file
        /// </summary>
        /// <param name="dirEntry">The directory entry of the file</param>
        /// <param name="size">The size of the thumbnail (vertical and horizontal pixel count)</param>
        /// <returns></returns>
        public async Task<byte[]> GetThumbnailImage(SeafDirEntry dirEntry, int size)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given directory entry is not a file.");

            return await GetThumbnailImage(dirEntry.LibraryId, dirEntry.Path, size);
        }

        /// <summary>
        ///     Get a thumbnail for the given file
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="path">Path to the file</param>
        /// <param name="size">The size of the thumbnail (vertical and horizontal pixel count)</param>
        /// <returns></returns>
        public async Task<byte[]> GetThumbnailImage(SeafLibrary library, string path, int size)
        {
            library.ThrowOnNull(nameof(library));

            return await GetThumbnailImage(library.Id, path, size);
        }

        /// <summary>
        ///     Get a thumbnail for the given file
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="path">Path to the file</param>
        /// <param name="size">The size of the thumbnail (vertical and horizontal pixel count)</param>
        /// <returns></returns>
        public async Task<byte[]> GetThumbnailImage(string libraryId, string path, int size)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            path.ThrowOnNull(nameof(path));
            size.ThrowOnNull(nameof(size));

            var request = new GetThumbnailImageRequest(AuthToken, libraryId, path, size);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Return the URL of the thumbnail for the given file and size
        /// </summary>
        /// <param name="libraryId">The id of the library the file is in</param>
        /// <param name="path">Path to the file</param>
        /// <param name="size">The size of the thumbnail (vertical and horizontal pixel count)</param>
        /// <returns></returns>
        public string GetThumbnailUrl(string libraryId, string path, int size)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            path.ThrowOnNull(nameof(path));
            size.ThrowOnNull(nameof(size));

            var request = new GetThumbnailImageRequest(AuthToken, libraryId, path, size);
            return ServerUri + request.CommandUri;
        }

        /// <summary>
        ///     Uploads a single file
        ///     Does not replace already existing files, instead the file will be renamed (e.g. test(1).txt if test.txt already
        ///     exists)
        ///     Use UpdateSingle to replace the contents of an already existing file
        /// </summary>
        /// <param name="library">The library the file should be uploaded to</param>
        /// <param name="targetDirectory">The directory the file should be uploaded to</param>
        /// <param name="targetFilename">The name of the file</param>
        /// <param name="fileContent">The new content of the file</param>
        /// <param name="progressCallback">Optional progress callback (will report percentage of upload)</param>
        public async Task<bool> UploadSingle(SeafLibrary library, string targetDirectory, string targetFilename, Stream fileContent, Action<float> progressCallback = null)
        {
            library.ThrowOnNull(nameof(library));

            return await UploadSingle(library.Id, targetDirectory, targetFilename, fileContent, progressCallback);
        }

        /// <summary>
        ///     Uploads a single file
        ///     Does not replace already existing files, instead the file will be renamed (e.g. test(1).txt if test.txt already
        ///     exists)
        ///     Use UpdateSingle to replace the contents of an already existing file
        /// </summary>
        /// <param name="libraryId">The id of the library the file should be uploaded to</param>
        /// <param name="targetDirectory">The directory the file should be uploaded to</param>
        /// <param name="targetFilename">The name of the file</param>
        /// <param name="fileContent">The new content of the file</param>
        /// <param name="progressCallback">Optional progress callback (will report percentage of upload)</param>
        public async Task<bool> UploadSingle(string libraryId, string targetDirectory, string targetFilename, Stream fileContent, Action<float> progressCallback = null)
        {
            // to upload files we need to get an upload link first            
            var req = new GetUploadLinkRequest(AuthToken, libraryId);
            var uploadLink = await _webConnection.SendRequestAsync(ServerUri, req);

            var uploadRequest = new UploadFilesRequest(AuthToken, uploadLink, targetDirectory, targetFilename, fileContent, progressCallback);
            return await _webConnection.SendRequestAsync(ServerUri, uploadRequest);
        }

        /// <summary>
        ///     Retrieve a link to upload files for the given library
        /// </summary>
        /// <param name="library">The library the file should be uploaded to</param>
        public async Task<string> GetUploadLink(SeafLibrary library)
        {
            library.ThrowOnNull(nameof(library));

            return await GetUploadLink(library.Id);
        }

        /// <summary>
        ///     Retrieve a link to upload files for the given library
        /// </summary>
        /// <param name="libraryId">The id of the library the file should be uploaded to</param>
        public async Task<string> GetUploadLink(string libraryId)
        {
            libraryId.ThrowOnNull(nameof(libraryId));

            // to upload files we need to get an upload link first            
            var request = new GetUploadLinkRequest(AuthToken, libraryId);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Update the contents of the given, existing file
        /// </summary>
        /// <param name="dirEntry">The file to update</param>
        /// <param name="fileContent">The new content of the file</param>
        /// <param name="progressCallback">Optional progress callback (will report percentage of upload)</param>
        public async Task<bool> UpdateSingle(SeafDirEntry dirEntry, Stream fileContent, Action<float> progressCallback = null)
        {
            dirEntry.ThrowOnNull(nameof(dirEntry));
            fileContent.ThrowOnNull(nameof(fileContent));

            if (dirEntry.Type != DirEntryType.File)
                throw new ArgumentException("The given dirEntry does not represent a file.");

            return await UpdateSingle(dirEntry.LibraryId, dirEntry.Directory, dirEntry.Name, fileContent, progressCallback);
        }

        /// <summary>
        ///     Update the contents of the given, existing file
        /// </summary>
        /// <param name="library">The library the file is in</param>
        /// <param name="targetDirectory">The directory the file is in</param>
        /// <param name="targetFilename">The name of the file</param>
        /// <param name="fileContent">The new content of the file</param>
        /// <param name="progressCallback">Optional progress callback (will report percentage of upload)</param>
        public async Task<bool> UpdateSingle(SeafLibrary library, string targetDirectory, string targetFilename, Stream fileContent, Action<float> progressCallback = null)
        {
            library.ThrowOnNull(nameof(library));

            return await UpdateSingle(library.Id, targetDirectory, targetFilename, fileContent, progressCallback);
        }

        /// <summary>
        ///     Update the contents of the given, existing file
        /// </summary>
        /// <param name="libraryId">Id of the library the file is in</param>
        /// <param name="targetDirectory">The directory the file is in</param>
        /// <param name="targetFilename">The name of the file</param>
        /// <param name="fileContent">The new content of the file</param>
        /// <param name="progressCallback">Optional progress callback (will report percentage of upload)</param>
        protected async Task<bool> UpdateSingle(string libraryId, string targetDirectory, string targetFilename, Stream fileContent, Action<float> progressCallback)
        {
            libraryId.ThrowOnNull(nameof(libraryId));
            targetDirectory.ThrowOnNull(nameof(targetDirectory));
            targetFilename.ThrowOnNull(nameof(targetFilename));
            fileContent.ThrowOnNull(nameof(fileContent));

            // to update files we need to get an update link first            
            var request = new GetUpdateLinkRequest(AuthToken, libraryId, targetDirectory);
            var uploadLink = await _webConnection.SendRequestAsync(ServerUri, request);

            var updateRequest = new UpdateFileRequest(AuthToken, uploadLink, targetDirectory, targetFilename, fileContent, progressCallback);
            return await _webConnection.SendRequestAsync(ServerUri, updateRequest);
        }

        /// <summary>
        ///     Provide the password for the given encrypted library
        ///     in order to be able to access its contents
        ///     (listing directory contents does NOT require the library to be decrypted)
        /// </summary>
        public async Task<bool> DecryptLibrary(SeafLibrary library, char[] password)
        {
            var request = new DecryptLibraryRequest(AuthToken, library.Id, password);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Returns a list of all files the user has marked as favorite (starred)
        /// </summary>
        public async Task<IList<SeafDirEntry>> ListStarredFiles()
        {
            var request = new ListStarredFilesRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Add the file to the list of starred files
        /// </summary>
        /// <param name="dirEntry">The file to star</param>
        public async Task<bool> StarFile(SeafDirEntry dirEntry)
        {
            var request = new StarFileRequest(AuthToken, dirEntry.LibraryId, dirEntry.Path);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        ///     Removes the file from the list of starred files
        /// </summary>
        /// <param name="dirEntry">The file to unstar</param>
        public async Task<bool> UnstarFile(SeafDirEntry dirEntry)
        {
            var request = new UnstarFileRequest(AuthToken, dirEntry.LibraryId, dirEntry.Path);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Lists all groups       
        /// </summary>
        /// <returns></returns>
        public async Task<SeafGroupList> ListGroups()
        {
            var request = new ListGroupsRequest(AuthToken);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Return the group information for the group with the given ID
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public async Task<SeafGroup> GetGroupInfo(int groupId)
        {
            // currently there is no way to retrieve information
            // for a single group, so get a list of all and extract the
            // required group
            var groups = await ListGroups();
            return groups.FirstOrDefault(x => x.Id == groupId);
        }

        /// <summary>
        /// Creates a new group with the given name and returns the group object
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public async Task<SeafGroup> AddGroup(String groupName)
        {
            var request = new AddGroupRequest(AuthToken, groupName);
            int groupId = await _webConnection.SendRequestAsync(ServerUri, request);            
            return await GetGroupInfo(groupId);
        }

        /// <summary>
        /// Changes the name of the given group to newName
        /// </summary>                
        public async Task<bool> RenameGroup(SeafGroup group, String newName)
        {
            return await RenameGroup(group.Id, newName);
        }

        /// <summary>
        /// Changes the name of the group with the given id to newName
        /// </summary>                
        public async Task<bool> RenameGroup(int groupId, String newName)
        {
            var request = new RenameGroupRequest(AuthToken, groupId, newName);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Deletes the group from the server
        /// </summary>
        /// <param name="group"></param>
        public async Task<bool> DeleteGroup(SeafGroup group)
        {
            return await DeleteGroup(group.Id);
        }

        /// <summary>
        /// Deletes the group which the specified group id from the server
        /// </summary>
        public async Task<bool> DeleteGroup(int groupId)
        {
            var request = new DeleteGroupRequest(AuthToken, groupId);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// List the members of the given group
        /// Only supported for Seafile server version 5.1.0+
        /// </summary>        
        public async Task<List<AccountInfo>> ListGroupMembers(SeafGroup group)
        {
            return await ListGroupMembers(group.Id);
        }

        /// <summary>
        /// List the members of the group with the given id
        /// Only supported for Seafile server version 5.1.0+
        /// </summary>        
        public async Task<List<AccountInfo>> ListGroupMembers(int groupId)
        {
            var request = new ListGroupMembersRequest(AuthToken, groupId);
            CheckRequestSupportedByServer(request);

            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Adds the given user as member to the group
        /// </summary>
        /// <param name="group">The group to add the user to</param>
        /// <param name="userAccount">The user to add</param>
        /// <returns></returns>
        public async Task<bool> AddGroupMember(SeafGroup group, AccountInfo userAccount)
        {
            return await AddGroupMember(group.Id, userAccount.Email);
        }

        /// <summary>
        /// Adds the user as member to the group
        /// </summary>
        /// <param name="group">The group to add the user to</param>
        /// <param name="userName">The login name of the user to add (i.e. the e-mail address)</param>
        /// <returns></returns>
        public async Task<bool> AddGroupMember(SeafGroup group, string userName)
        {
            return await AddGroupMember(group.Id, userName);
        }

        /// <summary>
        /// Adds the user as member to the group with the given id
        /// </summary>
        /// <param name="group">The group id of the group to add the user to</param>
        /// <param name="userName">The login name of the user to add (i.e. the e-mail address)</param>
        /// <returns></returns>
        public async Task<bool> AddGroupMember(int groupId, string userName)
        {
            var request = new AddGroupMemberRequest(AuthToken, groupId, userName);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Adds the given users as members to the given group
        /// </summary>
        /// <param name="group">The group to add the users to</param>
        /// <param name="users">Users to add</param>
        public async Task<BulkAddGroupMemberResponse> AddGroupMembers(SeafGroup group, IEnumerable<AccountInfo> users)
        {
            if (users == null) throw new ArgumentNullException(nameof(users));

            return await AddGroupMembers(group, users.Select(x => x.Email));
        }

        /// <summary>
        /// Adds the given users as members to the given group
        /// </summary>
        /// <param name="group">The group to add the users to</param>
        /// <param name="usernames">The login names of the users to add (i.e. the e-mail addresses)</param>
        public async Task<BulkAddGroupMemberResponse> AddGroupMembers(SeafGroup group, IEnumerable<string> usernames)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));            

            return await AddGroupMembers(group.Id, usernames);
        }

        /// <summary>
        /// Adds the given users as members to the the group with the given id
        /// </summary>
        /// <param name="groupId">The id of the group to add the users to</param>
        /// <param name="usernames">The login names of the users to add (i.e. the e-mail addresses)</param>
        public async Task<BulkAddGroupMemberResponse> AddGroupMembers(int groupId, IEnumerable<string> usernames)
        {
            if (usernames == null) throw new ArgumentNullException(nameof(usernames));

            BulkAddGroupMemberRequest request = new BulkAddGroupMemberRequest(AuthToken, groupId, usernames);
            CheckRequestSupportedByServer(request);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        /// <summary>
        /// Removes the given user from the group
        /// </summary>
        /// <param name="group">The group to remove the user from</param>
        /// <param name="userAccount">The user to remove</param>
        /// <returns></returns>
        public async Task<bool> RemoveGroupMember(SeafGroup group, AccountInfo userAccount)
        {
            return await RemoveGroupMember(group.Id, userAccount.Email);
        }

        /// <summary>
        /// Removes the user from the group
        /// </summary>
        /// <param name="group">The group to remove the user from</param>
        /// <param name="userName">The login name of the user to remove (i.e. the e-mail address)</param>
        /// <returns></returns>
        public async Task<bool> RemoveGroupMember(SeafGroup group, string userName)
        {
            return await RemoveGroupMember(group.Id, userName);
        }

        /// <summary>
        /// Removes the user from the group with the given id
        /// </summary>
        /// <param name="group">The group id of the group to remove the user from</param>
        /// <param name="userName">The login name of the user to remove (i.e. the e-mail address)</param>
        /// <returns></returns>
        public async Task<bool> RemoveGroupMember(int groupId, string userName)
        {
            var request = new RemoveGroupMemberRequest(AuthToken, groupId, userName);
            return await _webConnection.SendRequestAsync(ServerUri, request);
        }

        private void CheckRequestSupportedByServer(ISeafRequest request)
        {
            if (request.SupportedWithServerVersion(ServerVersion))            
                throw new InvalidOperationException("The request is not supportd by a server with version " + ServerVersion.ToString());            
        }

        /// <summary>
        /// Ends this seafile session
        /// (Note: as of now the Session Token remains active since there is no api call to invalidate it)
        /// </summary>
        public void Close()
        {
            _webConnection.Close();
        }
    }
}
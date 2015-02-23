using SeafClient.CommandParameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient
{
    enum SeafCommand
    {
        /// <summary>
        /// Command to receive an authentication token
        /// </summary>
        Authenticate,

        /// <summary>
        /// Return account info such as used and total space
        /// </summary>
        CheckAccountInfo,

        /// <summary>
        /// Return the users libraries (not including shared libraries)
        /// </summary>
        ListLibraries,

        /// <summary>
        /// Return the content of a directory in a library
        /// </summary>
        ListDirectoryEntries,

        /// <summary>
        /// Request download link for a file
        /// </summary>
        GetFileDownloadLink
    }

    static class SeafCommandExtensions
    {
        /// <summary>
        /// Return the http access type needed for the given command (e.g. POST, GET)
        /// </summary>        
        public static string GetAccessMethod(this SeafCommand command)
        {
            switch (command)
            {
                case SeafCommand.Authenticate:
                    return "POST";
                case SeafCommand.CheckAccountInfo:
                case SeafCommand.ListLibraries:
                case SeafCommand.ListDirectoryEntries:
                case SeafCommand.GetFileDownloadLink:
                    return "GET";
                default:
                    throw new ArgumentException("command");
            }
        }

        /// <summary>
        /// Return the seafile server uri which has to be called to execute the given command
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static String GetWebUri(this SeafCommand command)
        {
            switch (command)
            {
                case SeafCommand.Authenticate:
                    return "api2/auth-token/";
                case SeafCommand.CheckAccountInfo:
                    return "api2/account/info/";
                case SeafCommand.ListLibraries:
                    return "api2/repos/";
                case SeafCommand.ListDirectoryEntries:
                    return "api2/repos/{param:libraryid}/dir/";
                case SeafCommand.GetFileDownloadLink:
                    return "api2/repos/{param:libraryid}/file/";
                default:
                    throw new ArgumentException("command");
            }
        }

        /// <summary>
        /// Return the type of the class derived from SeafCommandParams which is needed
        /// for the given command
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static Type GetCommandParamType(this SeafCommand command)
        {
            switch (command)
            {
                case SeafCommand.Authenticate:
                    return typeof(AuthParams);
                case SeafCommand.ListDirectoryEntries:
                    return typeof(DirectoryParams);
                case SeafCommand.GetFileDownloadLink:
                    return typeof(FileParams);
                default:
                    return typeof(NoParams);
            }
        }
    }
}

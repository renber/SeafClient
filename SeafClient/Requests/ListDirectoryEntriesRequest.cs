using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request to receive the contents of a directory
    /// </summary>
    public class ListDirectoryEntriesRequest : SessionRequest<IList<SeafDirEntry>>
    {
        public string LibraryId { get; private set; }

        public String Path { get; private set; }

        public DirEntryFilter Filter { get; private set; }

        /// <summary>
        /// Defines if directory entries of sub directories should be returned as well
        /// </summary>
        public bool Recursive { get; set; }

        public override string CommandUri
        {
            get
            {
                if (Recursive)
                    return String.Format("api2/repos/{0}/dir/?p={1}&t=d&recursive=1", LibraryId, WebUtility.UrlEncode(Path));
                else
                {
                    switch (Filter)
                    {                        
                        case DirEntryFilter.OnlyFiles:
                            return String.Format("api2/repos/{0}/dir/?p={1}&t=f", LibraryId, WebUtility.UrlEncode(Path));
                        case DirEntryFilter.OnlyDirectories:
                            return String.Format("api2/repos/{0}/dir/?p={1}&t=d", LibraryId, WebUtility.UrlEncode(Path));
                        default:
                            return String.Format("api2/repos/{0}/dir/?p={1}", LibraryId, WebUtility.UrlEncode(Path));
                    }                    
                }
            }
        }

        public ListDirectoryEntriesRequest(string authToken, string libraryId, string path)
            : this(authToken, libraryId, path, DirEntryFilter.FilesAndDirectories, false)
        {
            // --
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="libraryId"></param>
        /// <param name="path"></param>
        /// <param name="recursive">When true, path will be searched recursively, but only directories will be returned</param>
        public ListDirectoryEntriesRequest(string authToken, string libraryId, string path, bool recursive)
            : this(authToken, libraryId, path, recursive ? DirEntryFilter.OnlyDirectories : DirEntryFilter.FilesAndDirectories, recursive)
        {
            // --
        }

        public ListDirectoryEntriesRequest(string authToken, string libraryId, string path, DirEntryFilter filter)
            : this(authToken, libraryId, path, filter, false)
        {
            // --
        }

        private ListDirectoryEntriesRequest(string authToken, string libraryId, string path, DirEntryFilter filter, bool recursive)
            : base(authToken)
        {
            if (authToken == null)
                throw new ArgumentNullException("authToken");
            if (libraryId == null)
                throw new ArgumentNullException("libraryId");
            if (path == null)
                throw new ArgumentNullException("path");

            LibraryId = libraryId;
            Path = path;

            if (!Path.StartsWith("/"))
                Path = "/" + Path;

            if (recursive && filter != DirEntryFilter.OnlyDirectories)
                throw new ArgumentException("If directory entries shall be returned recursively, filter has to be set to OnlyDirectories");

            Filter = filter;
            Recursive = recursive;
        }

        public override async System.Threading.Tasks.Task<IList<SeafDirEntry>> ParseResponseAsync(System.Net.Http.HttpResponseMessage msg)
        {
            var entries = await base.ParseResponseAsync(msg);

            // set the library id &  path of the items              
            foreach (var entry in entries)
            {
                entry.LibraryId = LibraryId;
                entry.Path = Path + entry.Name;
            }

            return entries;
        }

        public override SeafError GetSeafError(HttpResponseMessage msg)
        {
            switch (msg.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return new SeafError(msg.StatusCode, SeafErrorCode.PathDoesNotExist);                    
                case (HttpStatusCode)440:
                    return new SeafError(msg.StatusCode, SeafErrorCode.EncryptedLibrary_PasswordNotProvided);                    
                default:
                    return base.GetSeafError(msg);
            }
        }
    }

    public enum DirEntryFilter
    {
        FilesAndDirectories,
        OnlyFiles,
        OnlyDirectories
    }
}

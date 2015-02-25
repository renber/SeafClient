using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.CommandParameters
{
    /// <summary>
    /// Represents a directory or file inside a library
    /// </summary>
    class DirectoryEntryParams
    {
        public string LibraryId { get; set; }
        public string Path { get; set; }

        /// <summary>
        /// Needed for the CreateDirectory command,
        /// specifies that the directory shoudl be created
        /// </summary>
        public Boolean CreateNew { get; set; }

        public DirectoryEntryParams(string libraryId, string path, bool createNew = false)
        {
            LibraryId = libraryId;
            Path = path;

            CreateNew = createNew;
        }

        public IEnumerable<KeyValuePair<string, string>> ToList()
        {
            if (CreateNew)
                yield return new KeyValuePair<string, string>("operation", "mkdir");

            yield return new KeyValuePair<string, string>("libraryid", LibraryId);

            if (!String.IsNullOrEmpty(Path))
                yield return new KeyValuePair<string, string>("p", Path);
        }        
    }
}

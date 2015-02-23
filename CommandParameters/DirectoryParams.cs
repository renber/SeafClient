using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.CommandParameters
{
    /// <summary>
    /// Parameters for accessing a directory
    /// </summary>
    class DirectoryParams : SeafCommandParams
    {
        public string LibraryId { get; set; }
        public string Path { get; set; }

        public DirectoryParams(string libraryId, string path)
        {
            LibraryId = libraryId;
            Path = path;
        }

        public override IEnumerable<KeyValuePair<string, string>> ToList()
        {
            yield return new KeyValuePair<string, string>("libraryid", LibraryId);

            if (!String.IsNullOrEmpty(Path))
                yield return new KeyValuePair<string, string>("p", Path);
        }        
    }
}

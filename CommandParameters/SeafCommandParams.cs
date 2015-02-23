using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.CommandParameters
{
    /// <summary>
    /// Base class for seafile command parameter classes
    /// </summary>
    public abstract class SeafCommandParams
    {
        /// <summary>
        /// Can be used when no parameters are required for a command
        /// </summary>
        public static SeafCommandParams None = new NoParams();

        public abstract IEnumerable<KeyValuePair<string, string>> ToList();
    }
}

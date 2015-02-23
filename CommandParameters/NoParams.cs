using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.CommandParameters
{
    /// <summary>
    /// An empty SeafCommandParams instance
    /// </summary>
    public class NoParams : SeafCommandParams
    {
        public override IEnumerable<KeyValuePair<string, string>> ToList()
        {
            yield break;
        }
    }
}

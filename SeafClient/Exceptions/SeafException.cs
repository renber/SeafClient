using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SeafClient.Exceptions
{
    /// <summary>
    /// An exception in the seafile web api implementation
    /// </summary>
    public class SeafException : Exception
    {
        public HttpStatusCode Code { get; private set; }

        public SeafException(HttpStatusCode code, string reason)
            : base("[" + ((int)code).ToString() + " " + code.ToString() + "] " + reason)
        {
            Code = code;
        }
    }
}

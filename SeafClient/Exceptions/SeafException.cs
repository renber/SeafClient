using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SeafClient
{
    /// <summary>
    /// An exception in the seafile web api implementation
    /// </summary>
    public class SeafException : Exception
    {        
        public SeafError SeafError { get; private set; }

        public SeafException(SeafError seafError)
            : base(seafError.GetErrorMessage())
        {            
            SeafError = seafError;
        }
    }
}

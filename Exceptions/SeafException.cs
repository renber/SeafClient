using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeafClient.Exceptions
{
    public class SeafException : Exception
    {
        public int Code { get; private set; } 

        public SeafException(int code, string reason)
            : base(reason)
        {
            Code = code;
        }
    }
}

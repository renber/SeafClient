using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SeafClient.Types;

namespace SeafClient.Exceptions
{
    /// <summary>
    /// Special exception when the Seafile server returns 
    /// </summary>
    public class SeafTooManyRequestsException : SeafException
    {
        /// <summary>
        /// The time to wait before requests will be answered by the server
        /// </summary>
        public readonly TimeSpan WaitTime;

        /// <summary>
        /// Creates a new SeafTooManyRequestsException
        /// </summary>
        /// <param name="waitTime">The time to wait before requests will be answered by the server again (as reported by the server)</param>
        public SeafTooManyRequestsException(TimeSpan waitTime) : base(new SeafError((System.Net.HttpStatusCode)429, SeafErrorCode.TooManyRequests))
        {
            WaitTime = waitTime;
        }
    }
}

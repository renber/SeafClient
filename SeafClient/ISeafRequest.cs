using System;

namespace SeafClient
{
    /// <summary>
    ///     Base interface for all seafile requests
    /// </summary>
    public interface ISeafRequest
    {

        /// <summary>
        ///     The command uri for this command
        /// </summary>
        string CommandUri { get; }

        HttpAccessMethod HttpAccessMethod { get; }

        /// <summary>
        /// Indicates whether this request is supported by a seafile server with the given version or not
        /// </summary>        
        bool SupportedWithServerVersion(Version version);        

    }
}
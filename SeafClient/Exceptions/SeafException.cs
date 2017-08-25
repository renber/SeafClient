using System;
using SeafClient.Types;

namespace SeafClient.Exceptions
{
    /// <summary>
    /// An <see cref="Exception"/> in the seafile web api implementation
    /// </summary>
    public class SeafException : Exception
    {
        public SeafException(SeafError seafError) : base(seafError.GetErrorMessage())
        {
            SeafError = seafError;
        }

        public SeafError SeafError { get; }
    }
}
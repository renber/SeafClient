using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    /// <summary>
    /// Interface for classes which execute seafile web api requests
    /// </summary>
    public interface ISeafWebConnection
    {
        /// <summary>
        /// Execute the given Seafile request and return the response
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri">The server address</param>
        /// <param name="request">The request to send</param>        
        /// <returns>The response</returns>
        /// <exception cref="SeafException"></exception>
        Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request);

        /// <summary>
        /// Send the given request to the given seafile server
        /// </summary>
        /// <param name="serverUri">The server address</param>
        /// <param name="request">The request to send</param>
        /// <param name="timeout">User-defined request timeout (when timeouts are supported by this implementation of ISeafWebConnection) </param>
        /// <returns>The response</returns>
        /// <exception cref="SeafException"></exception>
        Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request, TimeSpan? timeout);

        /// <summary>
        /// Closes this ISeafWebConnection
        /// </summary>
        void Close();
    }
}

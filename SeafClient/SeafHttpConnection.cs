using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SeafClient.Exceptions;
using SeafClient.Utils;
using System.Collections.Generic;

namespace SeafClient
{
    /// <summary>
    /// Send requests to a seafile server using http(s)
    /// </summary>
    public class SeafHttpConnection : ISeafWebConnection
    {
        HttpClient client;
        
		/// <summary>
        /// Instantiate a SeafHttpConnection with default values
        /// </summary>
        public SeafHttpConnection()
            : this(null)
        {
            // --
        }

        /// <summary>
        /// Instantiate a SeafHttpConnection with a custom default timeout
        /// </summary>
        /// <param name="timeout">A custom timeout for all requests. When this is null the default timeout is used</param>
        public SeafHttpConnection(TimeSpan? timeout)
        {
            // set-up the HttpClient instance we are going to use for sending requests
            // we use the same instance for the whole lifetime of the session
            // since this is recommended by Microsoft
            // (see https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/calling-a-web-api-from-a-net-client)
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;

            client = new HttpClient(handler);
            if (timeout.HasValue)
                client.Timeout = timeout.Value;            
        }

        /// <summary>
        ///     Return an HttpRequestMessage which represents the given seafile request
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public HttpRequestMessage CreateHttpRequestMessage<T>(Uri serverUri, SeafRequest<T> request)
        {
            var targetUri = new Uri(serverUri, request.CommandUri);

            switch (request.HttpAccessMethod)
            {
                case HttpAccessMethod.Get:
                    return HttpUtils.CreateSimpleRequest(HttpMethod.Get, targetUri, request.GetAdditionalHeaders());
                case HttpAccessMethod.Post:
                    return HttpUtils.CreatePostRequest(targetUri, request.GetAdditionalHeaders(),
                        request.GetPostParameters());
                case HttpAccessMethod.Put:
                    return HttpUtils.CreatePutRequest(targetUri, request.GetAdditionalHeaders(),
                        request.GetPostParameters());
                case HttpAccessMethod.Delete:
                    return HttpUtils.CreateSimpleRequest(HttpMethod.Delete, targetUri, request.GetAdditionalHeaders());
                case HttpAccessMethod.Custom:
                    return request.GetCustomizedRequest(serverUri);
                default:
                    throw new ArgumentException("Unsupported method: " + request.HttpAccessMethod);
            }
        }
       
 
        /// <summary>
        /// Send the given request to the given seafile server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri">The server address</param>
        /// <param name="request">The request to send</param>
        /// <exception cref="SeafException"></exception>
        /// <returns>The response</returns>
        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request)
        {
            return await SendRequestAsync(serverUri, request, null);
        }
 
        /// <summary>
        /// Send the given request to the given seafile server
        /// </summary>
        /// <param name="serverUri">The server address</param>
        /// <param name="request">The request to send</param>
        /// <param name="timeout">User-defined request timeout (if non null)</param>
        /// <returns>The response</returns>
        /// <exception cref="SeafException"></exception>
        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request, TimeSpan? timeout)
        {
            if (client == null)
                throw new InvalidOperationException("This SeafHttpConnection has already been closed.");

            HttpRequestMessage requestMessage = CreateHttpRequestMessage(serverUri, request);

            HttpResponseMessage response;
            // when no custom timeout has been passed use the client's default timeout
            using (CancellationTokenSource cTokenSource = new CancellationTokenSource(timeout ?? client.Timeout))
            {
                response = await client.SendAsync(requestMessage, cTokenSource.Token);
            }

            if (request.WasSuccessful(response))
                return await request.ParseResponseAsync(response);
            else
                throw GetSeafException(request, response);                
        }

        SeafException GetSeafException<T>(SeafRequest<T> request, HttpResponseMessage response)
        {
            if (response.StatusCode == (System.Net.HttpStatusCode)429) // TooManyRequests
            {
                IEnumerable<string> values;
                if (response.Headers.TryGetValues("X-Throttle-Wait-Seconds", out values))                    
                {
                    int seconds;
                    if (int.TryParse(values.First(), out seconds))
                        return new SeafTooManyRequestsException(TimeSpan.FromSeconds(seconds));
                }

                // could not read the wait header
                return new SeafTooManyRequestsException(TimeSpan.FromSeconds(30));

            } else
                return new SeafException(request.GetSeafError(response));
        }

        /// <summary>
        /// Closes the underlying tcp/ip connection
        /// </summary>
        public void Close()
        {            
            client.Dispose();
            client = null;
        }
    }
}
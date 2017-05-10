using System;
using System.Net.Http;
using System.Threading.Tasks;
using SeafClient.Exceptions;
using SeafClient.Utils;

namespace SeafClient
{
    public class SeafHttpConnection : ISeafWebConnection
    {
        /// <summary>
        ///     Send the given request to the given seafile server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request)
        {
            var requestMessage = CreateHttpRequestMessage(serverUri, request);
            var handler = new HttpClientHandler { AllowAutoRedirect = false };

            HttpResponseMessage response;
            using (var client = new HttpClient(handler))
            {
                response = await client.SendAsync(requestMessage);
            }

            if (request.WasSuccessful(response))
                return await request.ParseResponseAsync(response);

            throw new SeafException(request.GetSeafError(response));
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
                case HttpAccessMethod.Delete:
                    return HttpUtils.CreateSimpleRequest(HttpMethod.Delete, targetUri, request.GetAdditionalHeaders());
                case HttpAccessMethod.Custom:
                    return request.GetCustomizedRequest(serverUri);
                default:
                    throw new ArgumentException("Unsupported method: " + request.HttpAccessMethod);
            }
        }
    }
}
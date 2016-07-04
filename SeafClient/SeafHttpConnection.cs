using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    public class SeafHttpConnection : ISeafWebConnection
    {
        /// <summary>
        /// Return an HttpRequestMessage which represents the given seafile request
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public HttpRequestMessage CreateHttpRequestMessage<T>(Uri serverUri, SeafRequest<T> request)
        {            
            Uri targetUri = new Uri(serverUri, request.CommandUri);            

            switch (request.HttpAccessMethod)
            {
                case HttpAccessMethod.Get:
                    return HttpUtils.CreateSimpleRequest(HttpMethod.Get, targetUri, request.GetAdditionalHeaders());
                case HttpAccessMethod.Post:
                    return HttpUtils.CreatePostRequest(targetUri, request.GetAdditionalHeaders(), request.GetPostParameters());
                case HttpAccessMethod.Delete:
                    return HttpUtils.CreateSimpleRequest(HttpMethod.Delete, targetUri, request.GetAdditionalHeaders());
                case HttpAccessMethod.Custom:
                    return request.GetCustomizedRequest(serverUri);
                default:
                    throw new ArgumentException("Unsupported method: " + request.HttpAccessMethod.ToString());
            }
        }

        /// <summary>
        /// Send the given request to the given seafile server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<T> SendRequestAsync<T>(Uri serverUri, SeafRequest<T> request)
        {            
            HttpRequestMessage requestMessage = CreateHttpRequestMessage(serverUri, request);

            HttpResponseMessage response;
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            using (HttpClient client = new HttpClient(handler))   
                response = await client.SendAsync(requestMessage);                            

            if (request.WasSuccessful(response))
                return await request.ParseResponseAsync(response);
            else
                throw new SeafException(request.GetSeafError(response));
        }
    }
}

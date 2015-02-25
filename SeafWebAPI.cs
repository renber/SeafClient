using Newtonsoft.Json;
using SeafClient.CommandParameters;
using SeafClient.Exceptions;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    public static class SeafWebAPI
    {

        /// <summary>
        /// Send the given request to the given seafile server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverUri"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<T> SendRequestAsync<T>(string serverUri, SeafRequest<T> request)
        {
            HttpResponseMessage response;

            if (!serverUri.EndsWith("/"))
                serverUri += "/";

            string targetUri = serverUri + request.CommandUri;

            switch (request.HttpAccessMethod)
            {
                case HttpAccessMethod.Get:
                    response = await HttpUtils.GetAsync(targetUri, request.GetAdditionalHeaders());
                    break;
                case HttpAccessMethod.Post:
                    response = await HttpUtils.PostAsync(targetUri, request.GetAdditionalHeaders(), request.GetPostParameters());
                    break;
                case HttpAccessMethod.Delete:
                    response = await HttpUtils.DeleteAsync(targetUri, request.GetAdditionalHeaders());
                    break;
                case HttpAccessMethod.Custom:
                    response = await request.SendRequestCustomizedAsync(serverUri);
                    break;
                default:
                    throw new ArgumentException("Unsupported method: " + request.HttpAccessMethod.ToString());
            }

            if (request.WasSuccessful(response))
                return await request.ParseResponseAsync(response);
            else
                throw new SeafException(response.StatusCode, request.GetErrorDescription(response));
        }
    }
}

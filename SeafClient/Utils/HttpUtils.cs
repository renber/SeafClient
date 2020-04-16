using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;

namespace SeafClient.Utils
{
    /// <summary>
    ///     Helper class for the <see cref="HttpClient"/> from System.Net.Http
    /// </summary>
    internal static class HttpUtils
    {
        /// <summary>
        ///     Creates an HTTP GET request to the given URI
        /// </summary>
        /// <param name="method">The HTTP method</param>
        /// <param name="uri">The uri to get</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP GET request</param>
        /// <returns>The http response</returns>
        public static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IEnumerable<KeyValuePair<string, string>> bodyParams)
        {
            var message = new HttpRequestMessage(method, uri);
            message.Headers.Referrer = uri;

            foreach (var hi in headerInfo)
                message.Headers.Add(hi.Key, hi.Value);

            if (bodyParams.Any())
            {
                message.Content = new FormUrlEncodedContent(bodyParams);
            }

            return message;
        }        
    }
}
using System;
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
        ///     Sends a HTTP GET request to the given URI
        /// </summary>
        /// <param name="method">The HTTP method</param>
        /// <param name="uri">The uri to get</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP GET request</param>
        /// <returns>The http response</returns>
        public static HttpRequestMessage CreateSimpleRequest(HttpMethod method, Uri uri, IEnumerable<KeyValuePair<string, string>> headerInfo)
        {
            var message = new HttpRequestMessage(method, uri);
            message.Headers.Referrer = uri;

            foreach (var hi in headerInfo)
                message.Headers.Add(hi.Key, hi.Value);

            return message;
        }

        /// <summary>
        ///     Sends a HTTP POST request to the given URI
        /// </summary>
        /// <param name="uri">The uri to post to</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP POST request</param>
        /// <param name="postParams">Post parameters</param>
        /// <returns>The http response</returns>
        public static HttpRequestMessage CreatePostRequest(Uri uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IEnumerable<KeyValuePair<string, string>> postParams)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Headers.Referrer = uri;

            foreach (var hi in headerInfo)
                message.Headers.Add(hi.Key, hi.Value);

            message.Content = new FormUrlEncodedContent(postParams);

            return message;
        }
    }
}
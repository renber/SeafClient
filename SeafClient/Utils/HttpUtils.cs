using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    /// <summary>
    /// Helper class for the HttpClient from System.Net.Http
    /// </summary>
    static class HttpUtils
    {
        /// <summary>
        /// Sends a HTTP GET request to the given URI
        /// </summary>
        /// <param name="uri">The uri to get</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP GET request</param>
        /// <param name="getParams">Parameters (will be added to the URI with ?key1=value1&key2=value2 etc.</param>
        /// <returns>The http response</returns>
        public static HttpRequestMessage CreateSimpleRequest(HttpMethod method, Uri uri, IEnumerable<KeyValuePair<string, string>> headerInfo)
        {
            HttpRequestMessage message = new HttpRequestMessage(method, uri);
            message.Headers.Referrer = uri;

            foreach (var hi in headerInfo)
                 message.Headers.Add(hi.Key, hi.Value);

            return message;            
        }

        /// <summary>
        /// Sends a HTTP POST request to the given URI
        /// </summary>
        /// <param name="uri">The uri to post to</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP POST request</param>
        /// <param name="getParams">Post parameters</param>
        /// <returns>The http response</returns>        
        public static HttpRequestMessage CreatePostRequest(Uri uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IEnumerable<KeyValuePair<string, string>> postParams)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Headers.Referrer = uri;

            foreach (var hi in headerInfo)
                message.Headers.Add(hi.Key, hi.Value);

            message.Content = new FormUrlEncodedContent(postParams);

            return message;
        }
    }
}

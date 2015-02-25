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
        public static async Task<HttpResponseMessage> GetAsync(string uri, IEnumerable<KeyValuePair<string, string>> headerInfo)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri(uri);

                foreach (var hi in headerInfo)
                    client.DefaultRequestHeaders.Add(hi.Key, hi.Value);                               

                HttpResponseMessage result = await client.GetAsync(uri);                

                string s = await result.Content.ReadAsStringAsync();
                return result;
            }
        }

        /// <summary>
        /// Sends a HTTP POST request to the given URI
        /// </summary>
        /// <param name="uri">The uri to post to</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP POST request</param>
        /// <param name="getParams">Post parameters</param>
        /// <returns>The http response</returns>
        public static async Task<HttpResponseMessage> PostAsync(string uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IEnumerable<KeyValuePair<string, string>> postParams)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri(uri);                

                foreach (var hi in headerInfo)
                    client.DefaultRequestHeaders.Add(hi.Key, hi.Value);

                HttpContent content = new FormUrlEncodedContent(postParams);                
                var result = await client.PostAsync(uri, content);

                string s = await result.Content.ReadAsStringAsync();
                return result;
            }
        }

        /// <summary>
        /// Sends a HTTP DELETE request to the given URI
        /// </summary>
        /// <param name="uri">The uri to delete</param>
        /// <param name="headerInfo">Additional headers to be added to the HTTP DELETE request</param>
        /// <param name="getParams">Delete parameters</param>
        /// <returns>The http response</returns>
        public static async Task<HttpResponseMessage> DeleteAsync(string uri, IEnumerable<KeyValuePair<string, string>> headerInfo)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri(uri);

                foreach (var hi in headerInfo)
                    client.DefaultRequestHeaders.Add(hi.Key, hi.Value);

                var result = await client.DeleteAsync(uri);

                string s = await result.Content.ReadAsStringAsync();
                return result;
            }
        }
    }

    /// <summary>
    /// Describes the result of an http request
    /// </summary>
    public class HttpResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }

        public HttpResponse(HttpStatusCode statusCode, string content)
        {
            StatusCode = statusCode;
            Content = content;
        }
    }
}

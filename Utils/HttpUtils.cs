using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    static class HttpUtils
    {

        public static async Task<HttpResponse> GetAsync(string uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IList<KeyValuePair<string, string>> getParams)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri(uri);

                foreach (var hi in headerInfo)
                    client.DefaultRequestHeaders.Add(hi.Key, hi.Value);
                
                // for http get, the params are appended to the uri
                for (int i = 0; i < getParams.Count; i++)                
                    uri += (i == 0 ? "?" : "&") + getParams[i].Key + "=" + getParams[i].Value;                

                var result = await client.GetAsync(uri);                

                string s = await result.Content.ReadAsStringAsync();
                return new HttpResponse(result.StatusCode, s);
            }
        }

        public static async Task<HttpResponse> PostAsync(string uri, IEnumerable<KeyValuePair<string, string>> headerInfo, IEnumerable<KeyValuePair<string, string>> postParams)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri(uri);

                foreach (var hi in headerInfo)
                    client.DefaultRequestHeaders.Add(hi.Key, hi.Value);

                HttpContent content = new FormUrlEncodedContent(postParams);            
                var result = await client.PostAsync(uri, content);

                string s = await result.Content.ReadAsStringAsync();
                return new HttpResponse(result.StatusCode, s);
            }
        }
    }

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

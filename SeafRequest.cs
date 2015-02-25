using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    /// <summary>
    /// Base class for Seafile web api requests
    /// </summary>    
    public abstract class SeafRequest<TResponse>
    {
        /// <summary>
        /// The command uri for this command
        /// </summary>
        public abstract string CommandUri { get; }

        /// <summary>
        /// The http method to execute this command with
        /// </summary>
        public abstract HttpAccessMethod HttpAccessMethod { get; }

        /// <summary>
        /// Get additional header values to send with this command
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<KeyValuePair<string, string>> GetAdditionalHeaders()
        {
            // we expect a respons ein JSON format
            yield return new KeyValuePair<string, string>("Accept", "application/json; indent=4");
        }

        /// <summary>
        /// Return the parameters to use when posting this command
        /// (only used if HttpAccessMethod is Post)
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            yield break;
        }

        /// <summary>
        /// Used to send the request when HttpAccessMethod is Custom
        /// </summary>
        /// <returns></returns>
        public virtual async Task<HttpResponseMessage> SendRequestCustomizedAsync(string serverUri)
        {
            throw new NotImplementedException("SendRequestCustomized has not been implemented for this request.");
        }
        
        /// <summary>
        /// Returns if the command was successful
        /// </summary>        
        public virtual bool WasSuccessful(HttpResponseMessage msg)
        {            
            return msg.StatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// Returns the error description for the given response message if the command
        /// was not executed successfully
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public virtual string GetErrorDescription(HttpResponseMessage msg)
        {
            switch ((int)msg.StatusCode)
            {
                case 520: // not a standard HTTP status code, but used by seafile
                    return "Operation failed.";
                default:
                    return "Unknown error.";
            }
        }

        /// <summary>
        /// Read the server response for this command
        /// </summary>        
        public virtual async Task<TResponse> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();

            // try to read the response content as JSON object
            return JsonConvert.DeserializeObject<TResponse>(content);
        }
    }

    public enum HttpAccessMethod
    {
        /// <summary>
        /// The request is sent using custom logic
        /// </summary>
        Custom,
        Get,
        Post,
        Delete
    }
}

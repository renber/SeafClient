using Newtonsoft.Json;
using SeafClient.CommandParameters;
using SeafClient.Exceptions;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    static class SeafWebAPI
    {

        /// <summary>
        /// Sends the given command to the seafile server and returns the result as JSON string
        /// </summary>
        public static async Task<T> SendCommandAsync<T>(string serverUri, string sessionToken, SeafCommand command, SeafCommandParams parameters = null)
        {
            if (parameters == null)
                parameters = SeafCommandParams.None;

            if (parameters.GetType() != command.GetCommandParamType())
                throw new ArgumentException("Wrong param type for command " + command.ToString());           

            if (!serverUri.EndsWith("/"))
                serverUri += "/";

            string targetUri = serverUri + command.GetWebUri();

            List<KeyValuePair<string, string>> pLst = new List<KeyValuePair<string, string>>(parameters.ToList());
            // some parameters go directly to the uri
            for (int i = pLst.Count - 1; i >= 0; i--)
            {
                string pName = "{param:"+pLst[i].Key+"}";
                if (targetUri.Contains(pName))
                {
                    targetUri = targetUri.Replace(pName, pLst[i].Value);
                    pLst.RemoveAt(i);
                }
            }
            
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
            if (!String.IsNullOrEmpty(sessionToken))
                headers.Add(new KeyValuePair<string,string>("Authorization", "Token " + sessionToken));

            headers.Add(new KeyValuePair<string,string>("Accept", "application/json; indent=4"));

            HttpResponse response;

            switch (command.GetAccessMethod())
            {
                case "POST":
                    response = await HttpUtils.PostAsync(targetUri, headers, pLst);
                    break;
                case "GET":
                    response = await HttpUtils.GetAsync(targetUri, headers, pLst);
                    break;
                default:
                    throw new Exception("Unsupported access method: " + command.GetAccessMethod());
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new SeafException((int)response.StatusCode, response.Content);
            }
            else
            {
                if (typeof(T) == typeof(string))
                    return (T)(object)response.Content.ToString();
                else
                    return JsonConvert.DeserializeObject<T>(response.Content);
            }                
        }

        /// <summary>
        /// Sends the given command to the seafile server and returns the result as JSON string
        /// without specifying an authentication token (only works with the Authenticate command)
        /// </summary>
        public static async Task<T> SendCommandAsync<T>(string serverUri, SeafCommand command, SeafCommandParams parameters)
        {
            return await SendCommandAsync<T>(serverUri, String.Empty, command, parameters);
        }
    }
}

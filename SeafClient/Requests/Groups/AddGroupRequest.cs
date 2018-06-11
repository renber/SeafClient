using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests.Groups
{
    /// <summary>
    /// Request to add a group. The id of the new group is returned upon success
    /// </summary>
    public class AddGroupRequest : SessionRequest<int>
    {
        public override string CommandUri => "api2/groups/";

        public override HttpAccessMethod HttpAccessMethod => HttpAccessMethod.Put;

        /// <summary>
        /// The name of the group to create
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Create a request to create a group with the given name
        /// </summary>
        /// <param name="groupName"></param>
        public AddGroupRequest(String authToken, string groupName)
            : base(authToken)
        {
            GroupName = groupName;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetPostParameters()
        {
            yield return new KeyValuePair<string, string>("group_name", GroupName);
        }

        public override async Task<int> ParseResponseAsync(HttpResponseMessage msg)
        {
            // try to read the response content as JSON object
            var stream = await msg.Content.ReadAsStreamAsync();
            using (StreamReader sr = new StreamReader(stream))
            {

#if DEBUG
                // print response text to the console in debug mode
                var responseStr = sr.ReadToEnd();
                Debug.WriteLine("RESPONSE to " + this.GetType().Name);
                Debug.WriteLine(responseStr);
                Debug.WriteLine("========");
                sr.BaseStream.Position = 0;
#endif

                string jsonStr = sr.ReadToEnd();
                var definition = new { group_id = 0, success = false };
                var result = JsonConvert.DeserializeAnonymousType(jsonStr, definition);

                // there is another success field in the json response we need to evaluate
                if (!result.success)
                    throw new InvalidDataException("The seafile server indicated that the group could not be created.");

                return result.group_id;
            }
        }
    }
}

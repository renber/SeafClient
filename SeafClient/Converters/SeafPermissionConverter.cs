using Newtonsoft.Json;
using SeafClient.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Converters
{
    /// <summary>
    /// JsonConverter 
    /// </summary>
    class SeafPermissionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(DateTime));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var s = serializer.Deserialize<string>(reader);
            
            switch (s)
            {
                case "r":
                    return SeafPermission.ReadOnly;
                case "rw":
                    return SeafPermission.ReadAndWrite;
                default:
                    throw new ArgumentException("Unknown permission string: " + s);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is SeafPermission)
            {
                string s;
                switch ((SeafPermission)value)
                {
                    case SeafPermission.ReadOnly:
                        s = "r";
                        break;
                    case SeafPermission.ReadAndWrite:
                        s = "rw";
                        break;
                    default:
                        throw new ArgumentException("Unknown permission: " + ((SeafPermission)value).ToString());
                }

                serializer.Serialize(writer, s);
            }
            else
                throw new InvalidOperationException("SeafPermissionConverter can only serialize SeafPermission objects.");
        }
    }
}

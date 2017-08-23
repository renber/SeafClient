using System;
using Newtonsoft.Json;
using SeafClient.Types;

namespace SeafClient.Converters
{
    /// <summary>
    ///     <see cref="JsonConverter"/> for converting the permission <see cref="string"/> to a <see cref="SeafPermission"/>
    /// </summary>
    internal class SeafPermissionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var permission = serializer.Deserialize<string>(reader);

            switch (permission)
            {
                case "r":
                    return SeafPermission.ReadOnly;
                case "rw":
                    return SeafPermission.ReadAndWrite;
                default:
                    throw new ArgumentException("Unknown permission string: " + permission);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is SeafPermission))
                throw new InvalidOperationException("SeafPermissionConverter can only serialize SeafPermission objects.");

            string permission;
            switch ((SeafPermission) value)
            {
                case SeafPermission.ReadOnly:
                    permission = "r";
                    break;
                case SeafPermission.ReadAndWrite:
                    permission = "rw";
                    break;
                default:
                    throw new ArgumentException("Unknown permission: " + (SeafPermission) value);
            }

            serializer.Serialize(writer, permission);
        }
    }
}
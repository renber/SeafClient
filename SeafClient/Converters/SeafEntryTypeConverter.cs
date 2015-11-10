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
    /// JsonConverter for converting the boolean JSON field 'dir' to a DirEntryType
    /// </summary>
    class SeafEntryTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(bool));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var isDir = serializer.Deserialize<bool>(reader);
            return isDir ? DirEntryType.Dir : DirEntryType.File;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DirEntryType)
                serializer.Serialize(writer, (DirEntryType)value == DirEntryType.Dir);
            else
                throw new InvalidOperationException("SeafEntryTypeConverter can only serialize DirEntryType values.");
        }
    }
}

using System;
using Newtonsoft.Json;
using SeafClient.Types;

namespace SeafClient.Converters
{
    /// <summary>
    ///     <see cref="JsonConverter"/> for converting the directory <see cref="bool"/> to a <see cref="DirEntryType"/>
    /// </summary>
    internal class SeafEntryTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(bool);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var isDir = serializer.Deserialize<bool>(reader);
            return isDir ? DirEntryType.Dir : DirEntryType.File;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is DirEntryType))
                throw new InvalidOperationException("SeafEntryTypeConverter can only serialize DirEntryType values.");

            serializer.Serialize(writer, (DirEntryType) value == DirEntryType.Dir);
        }
    }
}
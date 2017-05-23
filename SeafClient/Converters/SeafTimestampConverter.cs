using System;
using Newtonsoft.Json;
using SeafClient.Utils;

namespace SeafClient.Converters
{
    /// <summary>
    ///     <see cref="JsonConverter"/> for converting between <see cref="DateTime"/> and unix timestamps
    /// </summary>
    internal class SeafTimestampConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                var timestamp = serializer.Deserialize<long>(reader);
                return SeafDateUtils.SeafileTimeToDateTime(timestamp);
            }
            catch (JsonSerializationException)
            {
                // value is probably null
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is DateTime))
                throw new InvalidOperationException("SeafTimestampConverter can only serialize datetime objects.");

            serializer.Serialize(writer, SeafDateUtils.DateTimeToSeafileTime((DateTime) value));
        }
    }
}
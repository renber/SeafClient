using Newtonsoft.Json;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Converters
{
    /// <summary>
    /// JsonConverter for converting between dotnet datetimes and unix timestamps
    /// </summary>
    class SeafTimestampConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(DateTime));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var timestamp = serializer.Deserialize<long>(reader);
            return SeafDateUtils.SeafileTimeToDateTime(timestamp);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTime)
                serializer.Serialize(writer, SeafDateUtils.DateTimeToSeafileTime((DateTime)value));
            else
                throw new InvalidOperationException("SeafTimestampConverter can only serialize datetime objects.");
        }
    }
}

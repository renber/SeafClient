using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeafClient.Internal;

internal static class SeafJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        o.Converters.Add(new FlexibleTimestampConverter());
        o.Converters.Add(new NullableFlexibleTimestampConverter());
        o.Converters.Add(new FlexibleBooleanConverter());
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Seafile returned an empty {typeof(T).Name} payload.");

    public static async Task<T> DeserializeAsync<T>(Stream utf8Json, CancellationToken ct) =>
        await JsonSerializer.DeserializeAsync<T>(utf8Json, Options, ct).ConfigureAwait(false)
        ?? throw new JsonException($"Seafile returned an empty {typeof(T).Name} payload.");
}

/// <summary>
/// Seafile returns timestamps either as unix seconds (<c>mtime: 1794091693</c>) or ISO-8601 strings
/// (<c>"2026-11-07T22:47:47+00:00"</c>), sometimes for the same field on different endpoints.
/// </summary>
internal sealed class FlexibleTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                var n = reader.GetDouble();
                // Values beyond year ~5138 in seconds are almost certainly milliseconds.
                return n > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds((long)n) : DateTimeOffset.FromUnixTimeSeconds((long)n);
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return default;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var secs)) return DateTimeOffset.FromUnixTimeSeconds(secs);
                return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            case JsonTokenType.Null:
                return default;
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for a timestamp.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
}

/// <summary>Like <see cref="FlexibleTimestampConverter"/>, but maps null, "" and 0 to <c>null</c>.</summary>
internal sealed class NullableFlexibleTimestampConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly FlexibleTimestampConverter Inner = new();

    public override bool HandleNull => true;

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var value = Inner.Read(ref reader, typeof(DateTimeOffset), options);
        return value == default || value == DateTimeOffset.UnixEpoch ? null : value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value == null) writer.WriteNullValue();
        else Inner.Write(writer, value.Value, options);
    }
}

/// <summary>Seafile admin endpoints use 0/1 (and occasionally "true"/"false") for booleans.</summary>
internal sealed class FlexibleBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Null => false,
        JsonTokenType.Number => reader.GetDouble() != 0,
        JsonTokenType.String => IsTruthy(reader.GetString()),
        _ => throw new JsonException($"Unexpected token {reader.TokenType} for a boolean."),
    };

    private static bool IsTruthy(string? value)
    {
        if (value is null) return false;
        var span = value.AsSpan().Trim();
        return span.Equals("true", StringComparison.OrdinalIgnoreCase)
            || span.Equals("1", StringComparison.Ordinal)
            || span.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}

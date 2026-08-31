using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wanes.Shareds.Json;

/// <summary>
/// Every instant this API stores is UTC, and every timestamp it emits is UTC
/// written without an offset — clients are built to read a naked value as UTC.
/// Reading had no such rule: System.Text.Json turns "…T18:12:00Z" into a *local*
/// DateTime, so an inbound DepartAt landed in the database three hours off in a
/// UTC+3 deployment. Anything then compared against a genuinely-UTC column
/// (RequestedAt, ExpiresAt, DateTime.UtcNow) silently missed.
///
/// This closes the read side: offsets are converted to UTC, and a value with no
/// offset is taken at face value as UTC, matching what we write.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffffff";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Normalize(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(Normalize(value).ToString(Format, CultureInfo.InvariantCulture));

    internal static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <inheritdoc cref="UtcDateTimeConverter"/>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else Inner.Write(writer, value.Value, options);
    }
}

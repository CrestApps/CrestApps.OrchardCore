using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Calendars.Serialization;

/// <summary>
/// Writes and reads a wall-clock time as <c>yyyy-MM-ddTHH:mm:ss</c> with no offset, whatever its kind, and always reads it back
/// with an unspecified kind. The document store would otherwise treat an unspecified time as the server's local time and shift it.
/// </summary>
public sealed class WallClockDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// The format wall-clock times are written in.
    /// </summary>
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss";

    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Parse(reader.GetString());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }

    internal static DateTime Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return default;
        }

        // Drop any offset or 'Z' a writer added: the value is a wall-clock time, not a moment.
        var length = Math.Min(text.Length, 19);

        if (DateTime.TryParseExact(text.AsSpan(0, length), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ||
            DateTime.TryParseExact(text.AsSpan(0, Math.Min(text.Length, 10)), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }

        throw new JsonException($"'{text}' is not a wall-clock date and time.");
    }
}

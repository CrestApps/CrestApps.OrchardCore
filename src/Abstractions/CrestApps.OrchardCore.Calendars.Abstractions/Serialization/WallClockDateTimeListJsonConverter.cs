using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Calendars.Serialization;

/// <summary>
/// Writes and reads a list of wall-clock times the way <see cref="WallClockDateTimeJsonConverter"/> writes one.
/// </summary>
public sealed class WallClockDateTimeListJsonConverter : JsonConverter<IList<DateTime>>
{
    /// <inheritdoc />
    public override IList<DateTime> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<DateTime>();

        if (reader.TokenType == JsonTokenType.Null)
        {
            return result;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A list of wall-clock times must be an array.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            result.Add(WallClockDateTimeJsonConverter.Parse(reader.GetString()));
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IList<DateTime> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray();

        foreach (var item in value ?? [])
        {
            writer.WriteStringValue(item.ToString(WallClockDateTimeJsonConverter.Format, CultureInfo.InvariantCulture));
        }

        writer.WriteEndArray();
    }
}

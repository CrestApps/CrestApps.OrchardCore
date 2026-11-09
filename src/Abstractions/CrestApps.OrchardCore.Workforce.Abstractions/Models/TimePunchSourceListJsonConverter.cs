using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Reads a list of punch sources whose items are names or, as policies saved while sources were an enum hold them,
/// numbers, and writes the names.
/// </summary>
public sealed class TimePunchSourceListJsonConverter : JsonConverter<IList<string>>
{
    /// <inheritdoc/>
    public override IList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"A list of punch sources is an array, not a {reader.TokenType}.");
        }

        var sources = new List<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var source = TimePunchSourceJsonConverter.ReadSource(ref reader);

            if (!string.IsNullOrEmpty(source))
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IList<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray();

        foreach (var source in value ?? [])
        {
            writer.WriteStringValue(source);
        }

        writer.WriteEndArray();
    }
}

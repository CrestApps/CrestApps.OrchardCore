using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Reads a punch source stored as its name or, as punches saved while the source was an enum hold it, as a number, and
/// writes it as its name.
/// </summary>
public sealed class TimePunchSourceJsonConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadSource(ref reader);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStringValue(value);
    }

    internal static string ReadSource(ref Utf8JsonReader reader)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => TimePunchSource.FromLegacyValue(reader.GetInt64()),
            JsonTokenType.String => reader.GetString(),
            _ => throw new JsonException($"A punch source is a name or a number, not a {reader.TokenType}."),
        };
}

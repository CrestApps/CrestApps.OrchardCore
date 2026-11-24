using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// Reads a skill subject kind stored as its name or, as records saved while the kind was an enum hold it, as a number,
/// and writes it as its name.
/// </summary>
public sealed class SkillSubjectKindJsonConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => SkillSubjectKind.FromLegacyValue(reader.GetInt64()),
            JsonTokenType.String => SkillSubjectKind.Parse(reader.GetString()),
            _ => throw new JsonException($"A skill subject kind is a name or a number, not a {reader.TokenType}."),
        };

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
}

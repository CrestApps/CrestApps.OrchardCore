using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// Stores a <see cref="RecordRef"/> as the text <c>Kind:Id</c>, and an empty reference as <see langword="null"/>. It
/// also reads the object form <c>{ "Kind": ..., "Id": ... }</c>, so a reference written by hand in a recipe is read.
/// </summary>
public sealed class RecordRefJsonConverter : JsonConverter<RecordRef>
{
    /// <inheritdoc/>
    public override RecordRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return default;

            case JsonTokenType.String:
                return RecordRef.TryParse(reader.GetString(), out var record) ? record : default;

            case JsonTokenType.StartObject:
                string kind = null;
                string id = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    var name = reader.GetString();
                    reader.Read();

                    if (string.Equals(name, nameof(RecordRef.Kind), StringComparison.OrdinalIgnoreCase))
                    {
                        kind = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    }
                    else if (string.Equals(name, nameof(RecordRef.Id), StringComparison.OrdinalIgnoreCase))
                    {
                        id = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                return new RecordRef(kind, id);

            default:
                throw new JsonException($"A record reference is the text Kind:Id or an object, not a {reader.TokenType}.");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, RecordRef value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.IsEmpty)
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStringValue(value.ToString());
    }
}

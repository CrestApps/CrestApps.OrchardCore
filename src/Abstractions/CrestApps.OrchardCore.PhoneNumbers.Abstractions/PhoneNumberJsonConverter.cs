using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Serializes a <see cref="PhoneNumber"/> as the plain E.164 string it carries, so persisting a number as a
/// value object does not change the shape of any document or payload that already stores it as a string.
/// </summary>
public sealed class PhoneNumberJsonConverter : JsonConverter<PhoneNumber>
{
    /// <inheritdoc/>
    public override PhoneNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A phone number must be read from a string, but a {reader.TokenType} was found.");
        }

        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        // A stored value that is no longer in E.164 form is rejected rather than repaired, because a repair
        // here would invent a number the writer never recorded and every later comparison would be against
        // that invention. The rejection is a JsonException so the serializer reports it with the path to the
        // offending property instead of letting an argument exception escape without saying where it came from.
        if (!PhoneNumber.TryFromE164(value, out var phoneNumber))
        {
            throw new JsonException("A phone number must be stored in E.164 form.");
        }

        return phoneNumber;
    }

    /// <inheritdoc/>
    public override PhoneNumber ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        if (!PhoneNumber.TryFromE164(value, out var phoneNumber))
        {
            throw new JsonException("A phone number used as a property name must be stored in E.164 form.");
        }

        return phoneNumber;
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, PhoneNumber value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // The default value has no number, so it cannot name a property. Writing an empty name would make
        // every unknown number collide onto one entry, which is worse than refusing to write it.
        if (!value.HasValue)
        {
            throw new JsonException("A phone number with no value cannot be used as a property name.");
        }

        writer.WritePropertyName(value.Value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PhoneNumber value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!value.HasValue)
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStringValue(value.Value);
    }
}

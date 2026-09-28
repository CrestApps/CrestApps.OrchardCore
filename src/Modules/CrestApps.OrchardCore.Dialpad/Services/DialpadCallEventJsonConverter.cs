using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Dialpad.Services;

internal sealed class DialpadCallEventJsonConverter : JsonConverter<DialpadCallEvent>
{
    public override DialpadCallEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A Dialpad call event payload must be a JSON object.");
        }

        var externalNumber = ReadString(root, "external_number");
        var contactName = ReadString(root, "contact_name");

        if (root.TryGetProperty("contact", out var contact) &&
            contact.ValueKind == JsonValueKind.Object)
        {
            contactName ??= ReadString(contact, "name");
            externalNumber ??= ReadString(contact, "phone");
        }

        return new DialpadCallEvent
        {
            CallId = ReadFlexibleString(root, "call_id"),
            State = ReadString(root, "state"),
            Direction = ReadString(root, "direction"),
            ExternalNumber = externalNumber,
            InternalNumber = ReadString(root, "internal_number"),
            SelectedCallerId = ReadString(root, "selected_caller_id"),
            Target = ReadTarget(root, "target"),
            TargetId = ReadTargetProperty(root, "target", "id"),
            TargetType = ReadTargetProperty(root, "target", "type"),
            TargetEmail = ReadTargetProperty(root, "target", "email"),
            TargetPhone = ReadTargetProperty(root, "target", "phone"),
            TargetName = ReadTargetProperty(root, "target", "name"),
            ContactName = contactName,
            EventTimestamp = ReadNullableLong(root, "event_timestamp"),
            IsMuted = ReadNullableBoolean(root, "is_muted"),
            RecordingState = ReadString(root, "recording_state"),
            RecordingId = ReadFlexibleString(root, "recording_id"),
            IsConference = ReadNullableBoolean(root, "is_conference"),
            ParticipantCount = ReadNullableInt32(root, "participant_count"),
        };
    }

    public override void Write(Utf8JsonWriter writer, DialpadCallEvent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        WriteString(writer, "call_id", value.CallId);
        WriteString(writer, "state", value.State);
        WriteString(writer, "direction", value.Direction);
        WriteString(writer, "external_number", value.ExternalNumber);
        WriteString(writer, "internal_number", value.InternalNumber);
        WriteString(writer, "selected_caller_id", value.SelectedCallerId);
        WriteString(writer, "target", value.Target);
        WriteString(writer, "target_id", value.TargetId);
        WriteString(writer, "target_type", value.TargetType);
        WriteString(writer, "target_email", value.TargetEmail);
        WriteString(writer, "target_phone", value.TargetPhone);
        WriteString(writer, "target_name", value.TargetName);
        WriteString(writer, "contact_name", value.ContactName);
        WriteNullableInt64(writer, "event_timestamp", value.EventTimestamp);
        WriteNullableBoolean(writer, "is_muted", value.IsMuted);
        WriteString(writer, "recording_state", value.RecordingState);
        WriteString(writer, "recording_id", value.RecordingId);
        WriteNullableBoolean(writer, "is_conference", value.IsConference);
        WriteNullableInt32(writer, "participant_count", value.ParticipantCount);
        writer.WriteEndObject();
    }

    private static string ReadTarget(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.Object => ReadString(property, "phone")
                ?? ReadString(property, "name")
                ?? ReadString(property, "email")
                ?? ReadFlexibleString(property, "id"),
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a string, number, or object."),
        };
    }

    private static string ReadTargetProperty(JsonElement root, string propertyName, string targetPropertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return targetPropertyName == "id"
            ? ReadFlexibleString(property, targetPropertyName)
            : ReadString(property, targetPropertyName);
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The Dialpad '{propertyName}' container must be a JSON object.");
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => property.GetString(),
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a string."),
        };
    }

    private static string ReadFlexibleString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The Dialpad '{propertyName}' container must be a JSON object.");
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a string or number."),
        };
    }

    private static bool? ReadNullableBoolean(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The Dialpad '{propertyName}' container must be a JSON object.");
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a Boolean."),
        };
    }

    private static int? ReadNullableInt32(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The Dialpad '{propertyName}' container must be a JSON object.");
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a 32-bit integer."),
        };
    }

    private static long? ReadNullableLong(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The Dialpad '{propertyName}' container must be a JSON object.");
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when property.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new JsonException($"The Dialpad '{propertyName}' value must be a 64-bit integer."),
        };
    }

    private static void WriteNullableBoolean(Utf8JsonWriter writer, string propertyName, bool? value)
    {
        if (value.HasValue)
        {
            writer.WriteBoolean(propertyName, value.Value);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static void WriteNullableInt32(Utf8JsonWriter writer, string propertyName, int? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(propertyName, value.Value);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static void WriteNullableInt64(Utf8JsonWriter writer, string propertyName, long? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(propertyName, value.Value);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static void WriteString(Utf8JsonWriter writer, string propertyName, string value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }
}

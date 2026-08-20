using System.Globalization;
using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Parses the Telnyx call-event webhook envelope (<c>{ "data": { "event_type", "payload": { ... } } }</c>)
/// into the flattened <see cref="TelnyxCallEvent"/> the rest of the pipeline consumes.
/// </summary>
public static class TelnyxCallEventParser
{
    /// <summary>
    /// Attempts to parse a validated Telnyx webhook payload into a <see cref="TelnyxCallEvent"/>.
    /// </summary>
    /// <param name="payloadJson">The validated Telnyx webhook JSON body.</param>
    /// <param name="callEvent">When this method returns, contains the parsed event when successful.</param>
    /// <returns><see langword="true"/> when the payload was parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string payloadJson, out TelnyxCallEvent callEvent)
    {
        callEvent = null;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var eventType = ReadString(data, "event_type");

            if (string.IsNullOrWhiteSpace(eventType))
            {
                return false;
            }

            var payload = data.TryGetProperty("payload", out var payloadElement) && payloadElement.ValueKind == JsonValueKind.Object
                ? payloadElement
                : default;

            callEvent = new TelnyxCallEvent
            {
                EventType = eventType,
                EventId = ReadString(data, "id"),
                OccurredUtc = ReadDateTime(data, "occurred_at"),
                CallControlId = ReadString(payload, "call_control_id"),
                CallLegId = ReadString(payload, "call_leg_id"),
                CallSessionId = ReadString(payload, "call_session_id"),
                ConnectionId = ReadString(payload, "connection_id"),
                Direction = ReadString(payload, "direction"),
                From = ReadFrom(payload),
                To = ReadTo(payload),
                State = ReadString(payload, "state"),
                HangupCause = ReadString(payload, "hangup_cause"),
                HangupSource = ReadString(payload, "hangup_source"),
                SipHangupCause = ReadString(payload, "sip_hangup_cause"),
                RecordingId = ReadString(payload, "recording_id"),
                ClientState = ReadClientState(payload),
            };

            return !string.IsNullOrWhiteSpace(callEvent.CallControlId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadClientState(JsonElement payload)
    {
        // Telnyx transports client_state as a base64-encoded string it echoes back on every event for the leg.
        var raw = ReadString(payload, "client_state");

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        }
        catch (FormatException)
        {
            // A value we did not originate (or a non-base64 state) is not actionable; ignore it.
            return null;
        }
    }

    private static string ReadFrom(JsonElement payload)
    {
        // Telnyx reports the caller either as a scalar "from" or as a nested "from" object with a "phone_number".
        var scalar = ReadString(payload, "from");

        return !string.IsNullOrWhiteSpace(scalar) ? scalar : ReadNestedString(payload, "from", "phone_number");
    }

    private static string ReadTo(JsonElement payload)
    {
        var scalar = ReadString(payload, "to");

        return !string.IsNullOrWhiteSpace(scalar) ? scalar : ReadNestedString(payload, "to", "phone_number");
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string ReadNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(nestedPropertyName, out var nested) &&
            nested.ValueKind == JsonValueKind.String)
        {
            return nested.GetString();
        }

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed.UtcDateTime;
        }

        return null;
    }
}

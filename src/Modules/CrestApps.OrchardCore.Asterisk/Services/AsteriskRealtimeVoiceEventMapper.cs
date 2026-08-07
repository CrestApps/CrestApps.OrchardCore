using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal static class AsteriskRealtimeVoiceEventMapper
{
    public static bool TryMap(string providerName, string payload, out AsteriskRealtimeVoiceEvent voiceEvent)
    {
        voiceEvent = null;

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventType = ReadString(root, "type");

        if (string.IsNullOrWhiteSpace(eventType) ||
            !TryResolveChannel(root, out var channel))
        {
            return false;
        }

        var callId = ReadString(channel, "id");

        if (string.IsNullOrWhiteSpace(callId) ||
            !TryMapState(root, channel, eventType, out var state, out var isMuted, out var isOnHold))
        {
            return false;
        }

        var occurredUtc = TryReadDateTime(root, "timestamp");
        var metadata = BuildMetadata(root, channel, eventType);
        var callerNumber = ReadNestedString(channel, "caller", "number");
        var dialedNumber = ReadNestedString(channel, "dialplan", "exten") ?? ReadNestedString(channel, "connected", "number");
        var interactionCorrelationId = ReadChannelVariable(root, channel, AsteriskConstants.InteractionChannelVariableName);
        var isStasisStart = string.Equals(eventType, "StasisStart", StringComparison.OrdinalIgnoreCase);
        var isOwnedOrigination = isStasisStart &&
            (ContainsStasisArgument(root, AsteriskConstants.OriginationMarkerVariableName) ||
            !string.IsNullOrWhiteSpace(ReadChannelVariable(root, channel, AsteriskConstants.OriginationMarkerVariableName)));
        var isInbound = isStasisStart && !isOwnedOrigination;

        voiceEvent = new AsteriskRealtimeVoiceEvent
        {
            ProviderName = providerName,
            CallId = callId,
            EventType = eventType,
            FromAddress = callerNumber,
            ToAddress = ReadNestedString(channel, "connected", "number") ?? ReadNestedString(channel, "dialplan", "exten"),
            ChannelId = callId,
            ParentChannelId = ReadParentChannelId(root, channel),
            IsInbound = isInbound,
            IsOwnedOrigination = isOwnedOrigination,
            DialedNumber = dialedNumber,
            CallerNumber = callerNumber,
            InteractionCorrelationId = interactionCorrelationId,
            State = state,
            HangupCause = ResolveHangupCause(root, channel, state),
            IsMuted = isMuted,
            IsOnHold = isOnHold,
            OccurredUtc = occurredUtc,
            IdempotencyKey = BuildIdempotencyKey(providerName, eventType, root),
            IsConference = TryReadParticipantCount(root, out var participantCount)
                ? participantCount >= 2
                : null,
            ParticipantCount = participantCount,
            Metadata = metadata,
        };

        return true;
    }

    // Asterisk reports the release reason as a Q.850 cause code, which every hangup event carries alongside its
    // standard text. Resolving it here is what keeps a busy number, an unanswered dial, an abandoned call, and a
    // completed conversation distinguishable once the event has left the provider module.
    private static HangupCause? ResolveHangupCause(JsonElement root, JsonElement channel, CallState state)
    {
        if (state is not CallState.Disconnected and not CallState.Failed)
        {
            return null;
        }

        var wasAnswered = string.Equals(ReadString(channel, "state")?.Trim(), "Up", StringComparison.OrdinalIgnoreCase);
        var answerDetection = ReadChannelVariable(root, channel, AsteriskConstants.AnswerDetectionVariableName)?.Trim();
        var answeringMachineDetected = string.Equals(answerDetection, "MACHINE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(answerDetection, "FAX", StringComparison.OrdinalIgnoreCase);

        return AsteriskHangupCauseMapper.Resolve(
            TryReadInt32(root, "cause"),
            ReadString(root, "cause_txt"),
            wasAnswered,
            answeringMachineDetected);
    }

    private static int? TryReadInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static Dictionary<string, string> BuildMetadata(JsonElement root, JsonElement channel, string eventType)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["asteriskEventType"] = eventType,
        };

        var state = ReadString(channel, "state");

        if (!string.IsNullOrWhiteSpace(state))
        {
            metadata["asteriskState"] = state;
        }

        var application = ReadString(root, "application");

        if (!string.IsNullOrWhiteSpace(application))
        {
            metadata["asteriskApplication"] = application;
        }

        var bridgeId = ReadNestedString(root, "bridge", "id");

        if (!string.IsNullOrWhiteSpace(bridgeId))
        {
            metadata["bridgeId"] = bridgeId;
        }

        var cause = ReadString(root, "cause");

        if (!string.IsNullOrWhiteSpace(cause))
        {
            metadata["cause"] = cause;
        }

        var causeText = ReadString(root, "cause_txt");

        if (!string.IsNullOrWhiteSpace(causeText))
        {
            metadata["causeText"] = causeText;
        }

        var asteriskId = ReadString(root, "asterisk_id");

        if (!string.IsNullOrWhiteSpace(asteriskId))
        {
            metadata["asteriskId"] = asteriskId;
        }

        return metadata;
    }

    // ARI events carry no event id or sequence number, so the only stable identity available is the event content
    // itself. Hashing the raw wire bytes is fragile: a whitespace or property-order change from an Asterisk upgrade or
    // a proxy re-serialization would silently change the hash and defeat deduplication. Canonicalizing the payload
    // (recursively ordering object properties) before hashing pins the key to the event's meaning rather than its
    // formatting, so a re-serialized redelivery of the same event still deduplicates, while two genuinely distinct
    // same-type events on one call (which differ in at least one field — timestamp, varset value, or sub-state) keep
    // distinct keys and are both processed. The provider and event type are prefixed so the key is human-inspectable
    // and never collides across providers or event types.
    private static string BuildIdempotencyKey(string providerName, string eventType, JsonElement root)
    {
        var canonicalPayload = CanonicalizeJson(root);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));

        return $"{providerName}:{eventType}:{Convert.ToHexString(hash)}";
    }

    private static string CanonicalizeJson(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();

                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();

                break;
            case JsonValueKind.Number:
                // A parse/re-serialize proxy can rewrite a numeric token (1 vs 1.0 vs 1e1) without changing its value,
                // so the raw token must be normalized or dedupe would again depend on formatting. Integers round-trip
                // exactly through Int64/UInt64; everything else is normalized through the shortest round-trippable
                // double form. A number that fits none of these keeps its original token rather than risk a lossy
                // rewrite.
                if (element.TryGetInt64(out var int64Value))
                {
                    writer.WriteNumberValue(int64Value);
                }
                else if (element.TryGetUInt64(out var uint64Value))
                {
                    writer.WriteNumberValue(uint64Value);
                }
                else if (element.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue))
                {
                    writer.WriteNumberValue(doubleValue);
                }
                else
                {
                    element.WriteTo(writer);
                }

                break;
            default:
                element.WriteTo(writer);

                break;
        }
    }

    private static bool ContainsStasisArgument(JsonElement root, string argumentName)
    {
        if (!root.TryGetProperty("args", out var args) ||
            args.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var argument in args.EnumerateArray())
        {
            if (argument.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = argument.GetString();

            if (string.Equals(value, argumentName, StringComparison.OrdinalIgnoreCase) ||
                value?.StartsWith($"{argumentName}=", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadParentChannelId(JsonElement root, JsonElement channel)
        => ReadString(channel, "parent_channel_id") ??
            ReadString(channel, "parentChannelId") ??
            ReadString(root, "parent_channel_id") ??
            ReadString(root, "parentChannelId");

    // The Asterisk REST Interface declares channel variables on the Channel model as "channelvars", so that lookup has
    // to come first: it is the only location a conforming Asterisk release actually populates. The remaining lookups are
    // tolerated compatibility fallbacks for gateways that re-shape ARI payloads before they reach this mapper.
    private static string ReadChannelVariable(JsonElement root, JsonElement channel, string variableName)
        => ReadVariable(channel, "channelvars", variableName) ??
            ReadVariable(channel, "variables", variableName) ??
            ReadVariable(root, "variables", variableName) ??
            ReadVariable(root, "channelvars", variableName) ??
            ReadVariable(root, "channel_variables", variableName) ??
            ReadRootChannelVarset(root, variableName);

    private static string ReadVariable(JsonElement element, string variablesPropertyName, string variableName)
    {
        if (!element.TryGetProperty(variablesPropertyName, out var variables) ||
            variables.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in variables.EnumerateObject())
        {
            if (!string.Equals(property.Name, variableName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
        }

        return null;
    }

    private static string ReadRootChannelVarset(JsonElement root, string variableName)
    {
        var variable = ReadString(root, "variable");

        if (!string.Equals(variable, variableName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ReadString(root, "value");
    }

    private static bool TryResolveChannel(JsonElement root, out JsonElement channel)
    {
        if (root.TryGetProperty("channel", out channel))
        {
            return true;
        }

        if (root.TryGetProperty("peer", out channel))
        {
            return true;
        }

        channel = default;

        return false;
    }

    private static bool TryMapState(
        JsonElement root,
        JsonElement channel,
        string eventType,
        out CallState state,
        out bool? isMuted,
        out bool isOnHold)
    {
        state = CallState.Idle;
        isMuted = null;
        isOnHold = false;

        if (string.Equals(eventType, "ChannelHold", StringComparison.OrdinalIgnoreCase))
        {
            state = CallState.OnHold;
            isOnHold = true;

            return true;
        }

        if (string.Equals(eventType, "ChannelUnhold", StringComparison.OrdinalIgnoreCase))
        {
            state = CallState.Connected;

            return true;
        }

        if (string.Equals(eventType, "ChannelEnteredBridge", StringComparison.OrdinalIgnoreCase))
        {
            state = CallState.Connected;

            return true;
        }

        if (string.Equals(eventType, "ChannelLeftBridge", StringComparison.OrdinalIgnoreCase))
        {
            var channelState = ReadString(channel, "state");
            state = MapChannelState(channelState, isTerminalEvent: false);

            if (state == CallState.Idle ||
                string.Equals(channelState, "Down", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        if (string.Equals(eventType, "ChannelVarset", StringComparison.OrdinalIgnoreCase))
        {
            var variable = ReadString(root, "variable");
            var value = ReadString(root, "value");

            if (string.Equals(variable, AsteriskConstants.HoldStateVariableName, StringComparison.OrdinalIgnoreCase))
            {
                isOnHold = string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
                state = isOnHold ? CallState.OnHold : CallState.Connected;

                return true;
            }

            if (string.Equals(variable, AsteriskConstants.MuteStateVariableName, StringComparison.OrdinalIgnoreCase))
            {
                isMuted = string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
                state = MapChannelState(ReadString(channel, "state"), isTerminalEvent: false);

                if (state == CallState.Idle)
                {
                    state = CallState.Connected;
                }

                return true;
            }

            return false;
        }

        var terminalEvent = string.Equals(eventType, "ChannelDestroyed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "ChannelHangupRequest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "StasisEnd", StringComparison.OrdinalIgnoreCase);

        state = MapChannelState(ReadString(channel, "state"), terminalEvent);

        if (state != CallState.Idle)
        {
            isOnHold = state == CallState.OnHold;

            return true;
        }

        if (string.Equals(eventType, "StasisStart", StringComparison.OrdinalIgnoreCase))
        {
            state = CallState.Connecting;

            return true;
        }

        return false;
    }

    private static CallState MapChannelState(string channelState, bool isTerminalEvent)
    {
        if (isTerminalEvent)
        {
            return CallState.Disconnected;
        }

        return channelState?.Trim() switch
        {
            "Ring" => CallState.Ringing,
            "Ringing" => CallState.Ringing,
            "Up" => CallState.Connected,
            "Busy" => CallState.Failed,
            "Pre-ring" => CallState.Connecting,
            "Down" => CallState.Connecting,
            "Dialing" => CallState.Connecting,
            _ => CallState.Idle,
        };
    }

    private static bool TryReadParticipantCount(JsonElement root, out int? participantCount)
    {
        participantCount = null;

        if (!root.TryGetProperty("bridge", out var bridge) ||
            !bridge.TryGetProperty("channels", out var channels) ||
            channels.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        participantCount = channels.GetArrayLength();

        return true;
    }

    private static DateTime? TryReadDateTime(JsonElement element, string propertyName)
    {
        var text = ReadString(element, propertyName);

        if (string.IsNullOrWhiteSpace(text) ||
            !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value))
        {
            return null;
        }

        return value;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string ReadNestedString(JsonElement element, string parentPropertyName, string propertyName)
    {
        if (!element.TryGetProperty(parentPropertyName, out var parent))
        {
            return null;
        }

        return ReadString(parent, propertyName);
    }
}

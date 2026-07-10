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

        voiceEvent = new AsteriskRealtimeVoiceEvent
        {
            ProviderName = providerName,
            CallId = callId,
            EventType = eventType,
            FromAddress = ReadNestedString(channel, "caller", "number"),
            ToAddress = ReadNestedString(channel, "connected", "number") ?? ReadNestedString(channel, "dialplan", "exten"),
            State = state,
            IsMuted = isMuted,
            IsOnHold = isOnHold,
            OccurredUtc = occurredUtc,
            IdempotencyKey = BuildIdempotencyKey(providerName, payload),
            IsConference = TryReadParticipantCount(root, out var participantCount)
                ? participantCount > 2
                : null,
            ParticipantCount = participantCount,
            Metadata = metadata,
        };

        return true;
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

    private static string BuildIdempotencyKey(string providerName, string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return $"{providerName}:{Convert.ToHexString(hash)}";
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

using System.Text;
using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Reads and writes the Telnyx media-streaming WebSocket JSON protocol. Telnyx sends <c>connected</c>, <c>start</c>,
/// <c>media</c>, <c>dtmf</c>, and <c>stop</c> events; audio to inject back into the call is written as a <c>media</c>
/// event carrying the same base64 codec payload. In <c>rtp</c> bidirectional mode the payload is the raw codec
/// (mu-law) audio, so it maps directly onto a Contact Center media frame's data with no RTP header handling.
/// </summary>
internal static class TelnyxMediaStreamMessages
{
    /// <summary>The kinds of inbound frame the session acts on; everything else is ignored.</summary>
    public enum InboundKind
    {
        /// <summary>A frame the session ignores (connected, start, dtmf, mark, or an unparseable frame).</summary>
        Other,

        /// <summary>A media frame carrying decoded caller audio in <c>payload</c>.</summary>
        Media,

        /// <summary>A stop frame signalling Telnyx ended the stream.</summary>
        Stop,
    }

    /// <summary>
    /// The static <c>{"event":"clear"}</c> message that clears any audio Telnyx has buffered for playback.
    /// </summary>
    public static ReadOnlyMemory<byte> ClearMessage { get; } =
        Encoding.UTF8.GetBytes("{\"event\":\"clear\"}");

    /// <summary>
    /// Parses an inbound WebSocket text frame. For a <c>media</c> event the base64 <c>media.payload</c> is decoded
    /// into <paramref name="payload"/>; otherwise <paramref name="payload"/> is <see langword="null"/>. Malformed
    /// frames are reported as <see cref="InboundKind.Other"/> rather than throwing so one bad frame cannot tear down
    /// the read loop.
    /// </summary>
    public static InboundKind ReadInbound(ReadOnlySpan<byte> utf8Json, out byte[] payload)
    {
        payload = null;

        try
        {
            var reader = new Utf8JsonReader(utf8Json);
            using var document = JsonDocument.ParseValue(ref reader);

            if (!document.RootElement.TryGetProperty("event", out var eventProperty) ||
                eventProperty.ValueKind != JsonValueKind.String)
            {
                return InboundKind.Other;
            }

            var eventName = eventProperty.GetString();

            if (string.Equals(eventName, "stop", StringComparison.Ordinal))
            {
                return InboundKind.Stop;
            }

            if (!string.Equals(eventName, "media", StringComparison.Ordinal))
            {
                return InboundKind.Other;
            }

            if (document.RootElement.TryGetProperty("media", out var media) &&
                media.ValueKind == JsonValueKind.Object &&
                media.TryGetProperty("payload", out var payloadProperty) &&
                payloadProperty.ValueKind == JsonValueKind.String &&
                payloadProperty.TryGetBytesFromBase64(out var decoded) &&
                decoded.Length > 0)
            {
                payload = decoded;

                return InboundKind.Media;
            }

            return InboundKind.Other;
        }
        catch (JsonException)
        {
            return InboundKind.Other;
        }
    }

    /// <summary>
    /// Builds the UTF-8 bytes of a <c>media</c> message that injects <paramref name="payload"/> back into the call.
    /// </summary>
    public static byte[] CreateMediaMessage(ReadOnlySpan<byte> payload)
    {
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("event", "media");
            writer.WriteStartObject("media");
            writer.WriteBase64String("payload", payload);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }
}

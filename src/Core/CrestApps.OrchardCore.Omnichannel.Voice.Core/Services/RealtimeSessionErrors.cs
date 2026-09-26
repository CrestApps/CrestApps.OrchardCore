using System.Globalization;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What an error a realtime provider reports means for the call it arrived on.
/// </summary>
internal enum RealtimeSessionErrorKind
{
    /// <summary>
    /// The provider refused one request of ours — a cut past the end of a line, a response while one is already
    /// running — and the session is exactly as it was.
    /// </summary>
    Refused,

    /// <summary>
    /// Something went wrong that the session may well survive. It carries on, but a run of these ends it.
    /// </summary>
    Unexpected,

    /// <summary>
    /// The session is gone: expired, closed, or no longer allowed to run.
    /// </summary>
    Fatal,
}

/// <summary>
/// Sorts a realtime provider's error messages by what they mean for the call.
/// </summary>
/// <remarks>
/// The provider-neutral event carries only the message, so the message is what is read. Every error used to end
/// the call; live, the first one to arrive was a refusal of a truncation — nothing wrong with the session at all —
/// and the caller was left in fifty seconds of silence. The same refusals are treated as benign by the framework's
/// own browser-facing session runner.
/// </remarks>
internal static partial class RealtimeSessionErrors
{
    // Refusals of a single request: the session carries on unchanged.
    private static readonly string[] _refusals =
    [
        // "Audio content of 5150ms is already shorter than 5300ms": a cut past the end of the line.
        "already shorter",
        "audio_end_ms",

        // A response asked for while one is running, or a cancel that arrived after it had finished.
        "active response in progress",
        "no active response",
        "cancellation failed",

        // A commit with nothing in the buffer, which the detector can race.
        "buffer too small",
        "input_audio_buffer_commit_empty",

        // A setting the deployment does not support; the session keeps the one it has.
        "semantic_vad",
        "turn_detection",
        "eagerness",
        "cannot update a conversation's voice",
    ];

    // The session cannot continue.
    private static readonly string[] _fatal =
    [
        "session_expired",
        "expired",
        "maximum duration",
        "closed",
        "api key",
        "api_key",
        "unauthorized",
        "insufficient_quota",
    ];

    /// <summary>
    /// What an error means for the call.
    /// </summary>
    /// <param name="message">The provider's error message.</param>
    public static RealtimeSessionErrorKind Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return RealtimeSessionErrorKind.Unexpected;
        }

        if (Array.Exists(_refusals, marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return RealtimeSessionErrorKind.Refused;
        }

        return Array.Exists(_fatal, marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? RealtimeSessionErrorKind.Fatal
            : RealtimeSessionErrorKind.Unexpected;
    }

    /// <summary>
    /// Reads the two lengths out of the provider's refusal of a cut past the end of a line.
    /// </summary>
    /// <param name="message">The provider's error message.</param>
    /// <param name="heldMilliseconds">How much audio the provider says the line holds.</param>
    /// <param name="requestedMilliseconds">The cut that was asked for.</param>
    public static bool TryReadTruncationRefusal(string message, out int heldMilliseconds, out int requestedMilliseconds)
    {
        heldMilliseconds = 0;
        requestedMilliseconds = 0;

        var match = string.IsNullOrEmpty(message) ? null : TruncationRefusal().Match(message);

        return match is { Success: true } &&
            int.TryParse(match.Groups["held"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out heldMilliseconds) &&
            int.TryParse(match.Groups["requested"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out requestedMilliseconds);
    }

    [GeneratedRegex(@"(?<held>\d+)\s*ms\s+is\s+already\s+shorter\s+than\s+(?<requested>\d+)\s*ms", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex TruncationRefusal();
}

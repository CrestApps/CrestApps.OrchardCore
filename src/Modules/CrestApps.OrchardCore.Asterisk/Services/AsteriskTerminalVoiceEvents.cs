namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Identifies the real-time Asterisk event types that mean a channel has ended, so the resources allocated for
/// it can be released. This set is deliberately narrower than the one the state mapper treats as terminal, which
/// also includes <c>ChannelHangupRequest</c>: a hangup request is a request, and destroying bridges or hanging up
/// the peer leg on it would tear a conversation down before the channel actually ended. The two notions are kept
/// apart rather than shared for that reason.
/// </summary>
internal static class AsteriskTerminalVoiceEvents
{
    /// <summary>
    /// Determines whether the supplied real-time event type means the channel has ended.
    /// </summary>
    /// <param name="eventType">The Asterisk real-time event type.</param>
    /// <returns><see langword="true"/> when the event reports that the channel has ended.</returns>
    public static bool IsChannelGone(string eventType)
    {
        return string.Equals(eventType, "ChannelDestroyed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "StasisEnd", StringComparison.OrdinalIgnoreCase);
    }
}

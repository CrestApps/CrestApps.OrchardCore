namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Recognises the hangups with which Telnyx reports that a dialed number is not in service.
/// </summary>
/// <remarks>
/// A number that is not in service used to end like any other call: the release cause was not one the mapping
/// knew, so it read as a normal end, then as an abandoned call, and the number was dialed again on every retry
/// and every later load. Telnyx says it plainly, in one of two places. The normalized <c>hangup_cause</c> carries
/// <c>unallocated_number</c> or <c>not_found</c> (and, on some routes, the other tokens below), and
/// <c>sip_hangup_cause</c> carries the carrier's own SIP answer: 404 Not Found, 410 Gone (disconnected or
/// changed), 484 Address Incomplete (not a valid number) and 604 Does Not Exist Anywhere. The SIP answer is read
/// as well because the normalized cause is not always faithful: a carrier's 404 can arrive with a generic
/// cause.
/// </remarks>
public static class TelnyxNotInServiceCauses
{
    private static readonly HashSet<string> _hangupCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "unallocated_number",
        "not_found",
        "invalid_number_format",
        "invalid_number",
        "number_changed",
        "no_route_destination",
    };

    private static readonly HashSet<string> _sipHangupCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "404",
        "410",
        "484",
        "604",
    };

    /// <summary>
    /// Whether a hangup says the dialed number is not in service.
    /// </summary>
    /// <param name="hangupCause">The Telnyx <c>hangup_cause</c>.</param>
    /// <param name="sipHangupCause">The Telnyx <c>sip_hangup_cause</c>.</param>
    public static bool IsNotInService(string hangupCause, string sipHangupCause)
    {
        var cause = hangupCause?.Trim();

        if (!string.IsNullOrEmpty(cause) && _hangupCauses.Contains(cause))
        {
            return true;
        }

        var sipCause = sipHangupCause?.Trim();

        return !string.IsNullOrEmpty(sipCause) && _sipHangupCauses.Contains(sipCause);
    }

    /// <summary>
    /// Whether a call event is a hangup saying the dialed number is not in service.
    /// </summary>
    /// <param name="callEvent">The Telnyx call event.</param>
    public static bool IsNotInService(TelnyxCallEvent callEvent)
        => callEvent is not null &&
            string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase) &&
            IsNotInService(callEvent.HangupCause, callEvent.SipHangupCause);

    /// <summary>
    /// The provider's own words for the hangup, for the activity notes and the log: the cause and the SIP answer.
    /// </summary>
    /// <param name="hangupCause">The Telnyx <c>hangup_cause</c>.</param>
    /// <param name="sipHangupCause">The Telnyx <c>sip_hangup_cause</c>.</param>
    public static string Describe(string hangupCause, string sipHangupCause)
    {
        var cause = hangupCause?.Trim();
        var sipCause = sipHangupCause?.Trim();

        if (string.IsNullOrEmpty(sipCause))
        {
            return cause;
        }

        return string.IsNullOrEmpty(cause)
            ? $"SIP {sipCause}"
            : $"{cause} (SIP {sipCause})";
    }
}

using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Reads a hangup: whether an agent leg the platform originated never reached the agent, whether the target never
/// answered, and which <see cref="HangupCause"/> the provider's cause or the SIP response stands for.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    // Telnyx hangup causes that mean an agent leg the platform originated never reached the agent. It is a
    // superset of the no-answer causes: an endpoint that is not registered, or that declines, refuses the invite
    // outright rather than letting it ring out. NORMAL_CLEARING is deliberately absent -- that is the agent leg
    // of a real conversation ending, which is not a connect failure.
    private static readonly HashSet<string> _agentLegConnectFailureCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "NO_ANSWER",
        "USER_BUSY",
        "CALL_REJECTED",
        "REJECTED",
        "NORMAL_TEMPORARY_FAILURE",
        "UNALLOCATED_NUMBER",
        "INCOMPATIBLE_DESTINATION",
    };

    private static bool IsHangup(TelnyxCallEvent callEvent)
        => string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase);

    // SIP responses that mean an agent leg the platform originated was never answered: the endpoint timed out,
    // was unreachable, was busy, was unavailable, or declined the invite. These are read straight from the SIP
    // layer because the provider does not always normalize them faithfully -- an unreachable endpoint (for
    // example a lapsed registration answering 480) can arrive as NORMAL_CLEARING, which reads as an ordinary end
    // and would leave the customer on a call with an agent who was never reached. A leg that carried a real
    // conversation clears with 200, so it is never in this set.
    private static readonly HashSet<string> _agentLegConnectFailureSipCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "408",
        "480",
        "486",
        "503",
        "603",
    };

    private static bool IsAgentLegConnectFailure(TelnyxCallEvent callEvent)
    {
        if (!IsHangup(callEvent))
        {
            return false;
        }

        var cause = callEvent.HangupCause?.Trim();

        if (!string.IsNullOrEmpty(cause) && _agentLegConnectFailureCauses.Contains(cause))
        {
            return true;
        }

        var sipCause = callEvent.SipHangupCause?.Trim();

        return !string.IsNullOrEmpty(sipCause) && _agentLegConnectFailureSipCauses.Contains(sipCause);
    }

    // The recorded failure cause prefers the provider's normalized cause, but falls back to the SIP response when
    // the provider normalized an unreachable-endpoint response to a non-failure cause, so a released call is not
    // recorded as having ended normally.
    private static HangupCause? ResolveAgentLegFailureCause(TelnyxCallEvent callEvent)
    {
        var mapped = MapHangupCause(callEvent.HangupCause);

        if (mapped is null or HangupCause.NormalClearing)
        {
            var sipMapped = MapSipHangupCause(callEvent.SipHangupCause);

            if (sipMapped.HasValue)
            {
                return sipMapped;
            }
        }

        return mapped;
    }

    private static HangupCause? MapSipHangupCause(string sipHangupCause)
        => sipHangupCause?.Trim() switch
        {
            "408" or "480" => HangupCause.NoAnswer,
            "486" => HangupCause.Busy,
            "603" => HangupCause.Rejected,
            "503" => HangupCause.Failed,
            _ => null,
        };

    private static HangupCause? MapHangupCause(string hangupCause)
        => hangupCause?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "NORMAL_CLEARING" => Telephony.Models.HangupCause.NormalClearing,
            "TIMEOUT" or "NO_ANSWER" => Telephony.Models.HangupCause.NoAnswer,
            "USER_BUSY" => Telephony.Models.HangupCause.Busy,
            "CALL_REJECTED" or "REJECTED" => Telephony.Models.HangupCause.Rejected,
            "ORIGINATOR_CANCEL" or "CANCELED" or "CANCELLED" => Telephony.Models.HangupCause.Canceled,
            _ => Telephony.Models.HangupCause.Failed,
        };

    // Telnyx hangup causes that mean the target never answered (as opposed to a normal end after a conversation
    // or the caller canceling before answer). Only these route the caller to voicemail.
    private static readonly HashSet<string> _noAnswerHangupCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "NO_ANSWER",
        "USER_BUSY",
        "CALL_REJECTED",
        "NORMAL_TEMPORARY_FAILURE",
    };

    private static bool IsNoAnswerHangup(TelnyxCallEvent callEvent)
    {
        if (!string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(callEvent.HangupCause) &&
            _noAnswerHangupCauses.Contains(callEvent.HangupCause.Trim());
    }
}

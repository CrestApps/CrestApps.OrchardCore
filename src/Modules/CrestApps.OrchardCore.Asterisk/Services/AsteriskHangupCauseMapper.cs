using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Normalizes the release information an Asterisk channel reports when a call ends into the
/// provider-neutral <see cref="HangupCause"/> vocabulary.
/// <para>
/// Asterisk reports the release reason as a Q.850 cause code on the <c>cause</c> field of its
/// hangup events, with the standard text on <c>cause_txt</c>. Every one of those codes previously
/// collapsed into a single "disconnected" state, so a busy number, an unanswered dial, an abandoned
/// call, and a completed conversation were indistinguishable once the event left the mapper. That
/// erased outbound compliance reporting and abandon analytics at the source.
/// </para>
/// </summary>
internal static class AsteriskHangupCauseMapper
{
    /// <summary>
    /// Resolves the provider-neutral hangup cause for a terminated Asterisk channel.
    /// </summary>
    /// <param name="causeCode">The Q.850 cause code reported on the hangup event, when present.</param>
    /// <param name="causeText">The Q.850 cause text reported on the hangup event, when present.</param>
    /// <param name="wasAnswered">Whether the channel had reached the answered state before it was released.</param>
    /// <param name="answeringMachineDetected">Whether answer detection classified the answer as a machine or fax.</param>
    /// <returns>The provider-neutral hangup cause.</returns>
    public static HangupCause Resolve(
        int? causeCode,
        string causeText,
        bool wasAnswered,
        bool answeringMachineDetected)
    {
        if (answeringMachineDetected)
        {
            return HangupCause.AnsweringMachine;
        }

        if (causeCode.HasValue)
        {
            return FromCauseCode(causeCode.Value, wasAnswered);
        }

        if (TryResolveFromCauseText(causeText, wasAnswered, out var textCause))
        {
            return textCause;
        }

        return HangupCause.Unknown;
    }

    /// <summary>
    /// Maps a Q.850 cause code to the provider-neutral hangup cause.
    /// </summary>
    /// <param name="causeCode">The Q.850 cause code reported by Asterisk.</param>
    /// <param name="wasAnswered">Whether the channel had reached the answered state before it was released.</param>
    /// <returns>The provider-neutral hangup cause.</returns>
    public static HangupCause FromCauseCode(int causeCode, bool wasAnswered)
    {
        // Q.850 has no distinct "abandoned" cause. A caller who hangs up while the far end is still
        // alerting releases the channel with a normal cause, so an unanswered normal release is the
        // only signal that separates an abandoned call from a completed one.
        switch (causeCode)
        {
            case 16:
            case 31:
                return wasAnswered
                    ? HangupCause.NormalClearing
                    : HangupCause.Canceled;
            case 17:
                return HangupCause.Busy;
            case 18:
            case 19:
            case 20:
            case 102:
                return HangupCause.NoAnswer;
            case 21:
            case 22:
            case 23:
                return HangupCause.Rejected;
            case 34:
            case 38:
            case 41:
            case 42:
            case 44:
            case 47:
                return HangupCause.Congestion;
            case 0:
                return HangupCause.Unknown;
            default:
                return HangupCause.Failed;
        }
    }

    private static bool TryResolveFromCauseText(string causeText, bool wasAnswered, out HangupCause hangupCause)
    {
        hangupCause = HangupCause.Unknown;

        if (string.IsNullOrWhiteSpace(causeText))
        {
            return false;
        }

        switch (causeText.Trim().ToLowerInvariant())
        {
            case "normal clearing":
            case "normal, unspecified":
                hangupCause = wasAnswered
                    ? HangupCause.NormalClearing
                    : HangupCause.Canceled;

                return true;
            case "user busy":
                hangupCause = HangupCause.Busy;

                return true;
            case "no user responding":
            case "no answer from user (user alerted)":
            case "subscriber absent":
            case "recovery on timer expire":
                hangupCause = HangupCause.NoAnswer;

                return true;
            case "call rejected":
                hangupCause = HangupCause.Rejected;

                return true;
            case "no circuit/channel available":
            case "network out of order":
            case "switching equipment congestion":
            case "requested channel not available":
                hangupCause = HangupCause.Congestion;

                return true;
            default:
                return false;
        }
    }
}

using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How a dialer attempt ended, as the <c>DialerAttemptCompleted</c> event reports it to workflows and reports.
/// </summary>
/// <remarks>
/// Every attempt that did not reach an agent is dispositioned by the dialer itself with the disposition whose outcome
/// matches (see <c>DialerAttemptOutcomeHandler</c>), so the agent never receives it; the disposition's subject actions
/// and the <c>ActivityDispositionApplied</c> event then decide whether and when to try again. This outcome is also
/// reported on its own, for a workflow that reacts to the attempt rather than to the disposition.
/// </remarks>
public static class DialerAttemptOutcomes
{
    /// <summary>
    /// A person answered and an agent was connected to them.
    /// </summary>
    public const string Answered = "Answered";

    /// <summary>
    /// Nobody answered before the call rang out or was given up on.
    /// </summary>
    public const string NoAnswer = "NoAnswer";

    /// <summary>
    /// The line was busy.
    /// </summary>
    public const string Busy = "Busy";

    /// <summary>
    /// A voicemail, answering machine or fax picked up, and the call was screened out before an agent joined.
    /// </summary>
    public const string AnsweringMachine = "AnsweringMachine";

    /// <summary>
    /// The network reported the number not in service.
    /// </summary>
    public const string NotInService = "NotInService";

    /// <summary>
    /// The called party or the network declined the call.
    /// </summary>
    public const string Rejected = "Rejected";

    /// <summary>
    /// The call could not be completed for another reason, such as congestion or a failure on the provider's side.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// The customer answered, but the call ended before an agent was connected to it.
    /// </summary>
    public const string Disconnected = "Disconnected";

    /// <summary>
    /// The outcome of an ended attempt, from how the call ended, whether it was answered and whether an agent joined it.
    /// </summary>
    /// <param name="hangupCause">The call's normalized hangup cause.</param>
    /// <param name="answered">Whether the call was answered.</param>
    /// <param name="agentJoined">Whether an agent was connected to the answered call.</param>
    public static string Resolve(HangupCause? hangupCause, bool answered, bool agentJoined = true)
        => hangupCause switch
        {
            HangupCause.AnsweringMachine => AnsweringMachine,
            HangupCause.NotInService => NotInService,
            HangupCause.Busy => Busy,
            _ when answered => agentJoined ? Answered : Disconnected,
            HangupCause.Rejected => Rejected,
            HangupCause.Failed or HangupCause.Congestion => Failed,
            _ => NoAnswer,
        };

    /// <summary>
    /// Whether the outcome is an attempt that never reached an agent, which the dialer dispositions on its own.
    /// </summary>
    /// <param name="outcome">One of the outcomes above.</param>
    public static bool IsPreConnect(string outcome)
        => !string.IsNullOrEmpty(outcome) &&
            !string.Equals(outcome, Answered, StringComparison.Ordinal);
}

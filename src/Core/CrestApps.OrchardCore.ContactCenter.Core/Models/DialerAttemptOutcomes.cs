using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How a dialer attempt ended, as the <c>DialerAttemptCompleted</c> event reports it to workflows and reports.
/// </summary>
/// <remarks>
/// The dialer does not disposition an attempt nobody answered: it tries the record again. So the outcome of each
/// attempt is reported on its own, and a workflow that should act on an attempt -- text a customer the dialer could not
/// reach, for example -- reacts to that rather than to a disposition that never comes.
/// </remarks>
public static class DialerAttemptOutcomes
{
    /// <summary>
    /// A person answered.
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
    /// The outcome of an ended attempt, from how the call ended and whether it was answered.
    /// </summary>
    /// <param name="hangupCause">The call's normalized hangup cause.</param>
    /// <param name="answered">Whether the call was answered.</param>
    public static string Resolve(HangupCause? hangupCause, bool answered)
        => hangupCause switch
        {
            HangupCause.AnsweringMachine => AnsweringMachine,
            HangupCause.NotInService => NotInService,
            HangupCause.Busy => Busy,
            _ when answered => Answered,
            HangupCause.Rejected => Rejected,
            HangupCause.Failed or HangupCause.Congestion => Failed,
            _ => NoAnswer,
        };
}

using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The terminal reasons the dialer records on an activity it completed on its own, one per way an attempt can end
/// before an agent is connected, so a report can tell them apart from work an agent finished.
/// </summary>
public static class DialerTerminalReasons
{
    /// <summary>
    /// Nobody answered.
    /// </summary>
    public const string NoAnswer = "dialer_no_answer";

    /// <summary>
    /// The line was busy.
    /// </summary>
    public const string Busy = "dialer_busy";

    /// <summary>
    /// A machine answered and the call was screened out.
    /// </summary>
    public const string AnsweringMachine = "dialer_answering_machine";

    /// <summary>
    /// The called party or the network rejected the call.
    /// </summary>
    public const string Rejected = "dialer_rejected";

    /// <summary>
    /// The call failed, or the provider refused to place it.
    /// </summary>
    public const string Failed = "dialer_failed";

    /// <summary>
    /// The customer answered and hung up before an agent was connected.
    /// </summary>
    public const string Disconnected = "dialer_disconnected";

    /// <summary>
    /// The activity had used every attempt its dialer profile allows.
    /// </summary>
    public const string MaxAttemptsReached = "dialer_max_attempts";

    /// <summary>
    /// The terminal reason for an attempt outcome.
    /// </summary>
    /// <param name="outcome">One of <see cref="DialerAttemptOutcomes"/>.</param>
    public static string ForOutcome(string outcome)
        => outcome switch
        {
            DialerAttemptOutcomes.Busy => Busy,
            DialerAttemptOutcomes.AnsweringMachine => AnsweringMachine,
            DialerAttemptOutcomes.Rejected => Rejected,
            DialerAttemptOutcomes.Failed => Failed,
            DialerAttemptOutcomes.Disconnected => Disconnected,
            DialerAttemptOutcomes.NotInService => OmnichannelConstants.TerminalReasons.NumberNotInService,
            _ => NoAnswer,
        };

    /// <summary>
    /// The disposition outcome that stands for an attempt outcome.
    /// </summary>
    /// <param name="outcome">One of <see cref="DialerAttemptOutcomes"/>.</param>
    public static DispositionOutcome ToDispositionOutcome(string outcome)
        => outcome switch
        {
            DialerAttemptOutcomes.Busy => DispositionOutcome.Busy,
            DialerAttemptOutcomes.AnsweringMachine => DispositionOutcome.AnsweringMachine,
            DialerAttemptOutcomes.Rejected => DispositionOutcome.Rejected,
            DialerAttemptOutcomes.Failed => DispositionOutcome.Failed,
            DialerAttemptOutcomes.Disconnected => DispositionOutcome.Disconnected,
            DialerAttemptOutcomes.NotInService => DispositionOutcome.NotInService,
            _ => DispositionOutcome.NoAnswer,
        };
}

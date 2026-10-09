namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// The moments of an automated voice call an <see cref="IAutomatedVoiceCallObserver"/> is told about.
/// </summary>
public enum AutomatedVoiceCallObservationKind
{
    /// <summary>
    /// The call was answered and the automated agent is about to speak.
    /// </summary>
    Answered,

    /// <summary>
    /// The provider said who answered: a person or a machine.
    /// </summary>
    AnswererDetected,

    /// <summary>
    /// The automated agent's part of the call ended.
    /// </summary>
    ConversationEnded,

    /// <summary>
    /// The automated agent told the person the call is recorded, in the tenant's words.
    /// </summary>
    RecordingDisclosed,

    /// <summary>
    /// The automated agent was to tell the person the call is recorded and did not: its opening line did not contain
    /// the tenant's words, or it never spoke.
    /// </summary>
    RecordingDisclosureMissed,
}

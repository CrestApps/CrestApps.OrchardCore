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
}

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// One moment of an automated voice call, as reported to an <see cref="IAutomatedVoiceCallObserver"/>.
/// </summary>
public sealed class AutomatedVoiceCallObservation
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public AutomatedVoiceCallObservationKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider carrying the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's id for the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets who answered, for <see cref="AutomatedVoiceCallObservationKind.AnswererDetected"/>: Person,
    /// Machine or Unknown.
    /// </summary>
    public string Answerer { get; set; }

    /// <summary>
    /// Gets or sets how the conversation ended, for <see cref="AutomatedVoiceCallObservationKind.ConversationEnded"/>,
    /// such as Completed, HandedToAgent or NotConcluded.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets the disposition the conversation was concluded with, when it was concluded.
    /// </summary>
    public string DispositionId { get; set; }
}

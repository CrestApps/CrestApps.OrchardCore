namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How an interaction turned out, as every Contact Center report counts it. Unlike
/// <see cref="CrestApps.OrchardCore.ContactCenter.Models.InteractionStatus"/>, which records where the communication
/// session is, the outcome answers what happened to the contact, and each interaction has exactly one.
/// </summary>
public enum InteractionOutcome
{
    /// <summary>
    /// The interaction has not reached an outcome yet.
    /// </summary>
    InProgress,

    /// <summary>
    /// An agent answered the interaction.
    /// </summary>
    Answered,

    /// <summary>
    /// The customer hung up while waiting in a queue or while an agent was being offered the call, before anybody
    /// answered it.
    /// </summary>
    Abandoned,

    /// <summary>
    /// The platform sent the caller to voicemail instead of connecting them: the offer went unanswered, the caller
    /// waited as long as the queue allows, the entry point was closed, or the queue was full.
    /// </summary>
    Voicemail,

    /// <summary>
    /// The interaction failed for a technical reason: the provider reported an error, or the call could not be
    /// placed or bridged.
    /// </summary>
    Failed,

    /// <summary>
    /// An outbound attempt ended without the other party answering.
    /// </summary>
    NotConnected,

    /// <summary>
    /// The caller accepted the queue's offer to be called back instead of waiting. Nobody answered this call, and
    /// the caller did not give up on it either: the contact continues as the scheduled callback, so it is neither
    /// answered nor abandoned and is left out of the service level.
    /// </summary>
    CallbackRequested,
}

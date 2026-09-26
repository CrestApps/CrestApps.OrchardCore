using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a message in a queue's shared voicemail box: a voicemail left on a queue line that belongs to the
/// queue's team rather than to one agent, so any entitled member can review it, claim it and act on it.
/// </summary>
/// <remarks>
/// The recording itself stays on the interaction the caller left it on, and is played and erased through recording
/// governance like every other recording. This record carries only what the team needs to share the work: which queue
/// it is for, who called, and who is handling it.
/// </remarks>
public sealed class SharedVoicemail : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction the caller left the message on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue whose shared box holds the message.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the number the caller called from.
    /// </summary>
    public string CallerNumber { get; set; }

    /// <summary>
    /// Gets or sets the name of the contact the caller was recognized as, when they were.
    /// </summary>
    public string CallerName { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the contact the caller was recognized as, when they were.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the contact the caller was recognized as, when they were.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the caller was sent to voicemail.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets where the message is in its handling.
    /// </summary>
    public SharedVoicemailStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of whoever is handling the message.
    /// </summary>
    public string ClaimedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever is handling the message.
    /// </summary>
    public string ClaimedByUserName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was claimed.
    /// </summary>
    public DateTime? ClaimedUtc { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of whoever marked the message as dealt with.
    /// </summary>
    public string ResolvedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever marked the message as dealt with.
    /// </summary>
    public string ResolvedByUserName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was marked as dealt with.
    /// </summary>
    public DateTime? ResolvedUtc { get; set; }

    /// <summary>
    /// Gets or sets the note left when the message was marked as dealt with.
    /// </summary>
    public string ResolutionNote { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the last callback requested for the caller.
    /// </summary>
    public string CallbackRequestId { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever last asked for the caller to be called back.
    /// </summary>
    public string CallbackRequestedByUserName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the caller was last asked to be called back.
    /// </summary>
    public DateTime? CallbackRequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the record was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

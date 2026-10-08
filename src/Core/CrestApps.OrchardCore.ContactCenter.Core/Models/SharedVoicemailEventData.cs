using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Something that happened to a message in a queue's shared voicemail box: the payload of the shared voicemail
/// events, such as <see cref="ContactCenterConstants.Events.SharedVoicemailClaimed"/>.
/// </summary>
public sealed class SharedVoicemailEventData
{
    /// <summary>
    /// Gets or sets the shared voicemail the event is about.
    /// </summary>
    public string SharedVoicemailId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the caller left the message on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the queue whose shared box holds the message.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the status the message was in before the change.
    /// </summary>
    public SharedVoicemailStatus PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the status the message is in after the change.
    /// </summary>
    public SharedVoicemailStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of whoever made the change, or <see langword="null"/> for the platform.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever made the change.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of whoever held the message before the change, when it was claimed.
    /// </summary>
    public string PreviousClaimedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the note recorded with the change.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the callback the change requested, when it requested one.
    /// </summary>
    public string CallbackRequestId { get; set; }

    /// <summary>
    /// Gets or sets when the change happened.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}

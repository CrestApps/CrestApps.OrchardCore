using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a user may do with the queue shared voicemail boxes: whether they may use them at all, which queues' boxes
/// they may see, and whether they may manage messages beyond their own handling.
/// </summary>
public sealed class SharedVoicemailAccess
{
    /// <summary>
    /// Gets the access of a user who may not use the shared voicemail boxes.
    /// </summary>
    public static SharedVoicemailAccess None { get; } = new();

    /// <summary>
    /// Gets or sets the Orchard user identifier of the user.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets or sets the user name of the user.
    /// </summary>
    public string UserName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may use the shared voicemail boxes at all.
    /// </summary>
    public bool CanAccess { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may see every queue's box, rather than only those of the
    /// queues in <see cref="QueueIds"/>.
    /// </summary>
    public bool AllQueues { get; init; }

    /// <summary>
    /// Gets or sets the queues whose boxes the user may see, when <see cref="AllQueues"/> is not set.
    /// </summary>
    public IReadOnlyCollection<string> QueueIds { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the user may delete messages and act on messages somebody else has claimed.
    /// </summary>
    public bool CanManage { get; init; }

    /// <summary>
    /// Determines whether the user may see the box of a queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <returns><see langword="true"/> when the user may see the queue's box.</returns>
    public bool CoversQueue(string queueId)
        => CanAccess &&
            !string.IsNullOrEmpty(queueId) &&
            (AllQueues || QueueIds.Contains(queueId, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Determines whether the user is the one handling a message.
    /// </summary>
    /// <param name="voicemail">The message.</param>
    /// <returns><see langword="true"/> when the user holds the message's claim.</returns>
    public bool Holds(SharedVoicemail voicemail)
        => voicemail?.Status == SharedVoicemailStatus.Claimed && IsUser(voicemail.ClaimedByUserId);

    /// <summary>
    /// Determines whether the user may claim a message: an unclaimed one, or, for a user who manages shared voicemail,
    /// one somebody else holds.
    /// </summary>
    /// <param name="voicemail">The message.</param>
    /// <returns><see langword="true"/> when the user may claim the message.</returns>
    public bool CanClaim(SharedVoicemail voicemail)
        => CoversQueue(voicemail?.QueueId) &&
            voicemail.Status != SharedVoicemailStatus.Resolved &&
            !Holds(voicemail) &&
            (voicemail.Status == SharedVoicemailStatus.New || CanManage);

    /// <summary>
    /// Determines whether the user may return a claimed or resolved message to the team: their own, or anybody's for a
    /// user who manages shared voicemail.
    /// </summary>
    /// <param name="voicemail">The message.</param>
    /// <returns><see langword="true"/> when the user may return the message.</returns>
    public bool CanRelease(SharedVoicemail voicemail)
        => CoversQueue(voicemail?.QueueId) &&
            voicemail.Status != SharedVoicemailStatus.New &&
            (Holds(voicemail) || IsUser(voicemail.ResolvedByUserId) || CanManage);

    /// <summary>
    /// Determines whether the user may mark a message as dealt with, or have its caller called back: an unclaimed
    /// message or their own, or anybody's for a user who manages shared voicemail.
    /// </summary>
    /// <param name="voicemail">The message.</param>
    /// <returns><see langword="true"/> when the user may work the message.</returns>
    public bool CanWork(SharedVoicemail voicemail)
        => CoversQueue(voicemail?.QueueId) &&
            voicemail.Status != SharedVoicemailStatus.Resolved &&
            (voicemail.Status == SharedVoicemailStatus.New || Holds(voicemail) || CanManage);

    /// <summary>
    /// Determines whether the user may delete a message.
    /// </summary>
    /// <param name="voicemail">The message.</param>
    /// <returns><see langword="true"/> when the user may delete the message.</returns>
    public bool CanDelete(SharedVoicemail voicemail)
        => CanManage && CoversQueue(voicemail?.QueueId);

    private bool IsUser(string candidate)
        => !string.IsNullOrEmpty(candidate) &&
            !string.IsNullOrEmpty(UserId) &&
            string.Equals(candidate, UserId, StringComparison.OrdinalIgnoreCase);
}

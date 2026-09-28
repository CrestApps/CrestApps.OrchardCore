using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Takes a caller who is still waiting in a queue out of it and into voicemail. Queues decide when a caller has
/// waited long enough; only a voice feature can actually move the live call, so the queues feature registers a
/// sink that declines and the voice feature replaces it with one that can.
/// </summary>
public interface IWaitingCallVoicemailSink
{
    /// <summary>
    /// Sends the waiting caller behind a queue item to voicemail, removing the item from its queue.
    /// </summary>
    /// <param name="item">The waiting queue item.</param>
    /// <param name="reasonCode">The reason recorded against the activity and interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the caller was sent to voicemail; <see langword="false"/> when the item is not a
    /// waiting voice call this deployment can move, in which case it is left where it is.
    /// </returns>
    Task<bool> SendToVoicemailAsync(QueueItem item, string reasonCode, CancellationToken cancellationToken = default);
}

using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Copies the pictures of a received message out of the provider and into the workspace's own attachment store.
/// A provider hosts inbound media only for a while, and some only for callers holding its credentials, so a picture
/// that is not copied when it arrives cannot be shown later.
/// </summary>
public interface IMessagingInboundMediaIngestor
{
    /// <summary>
    /// Fetches and stores the media the provider announced on <see cref="OmnichannelMessage.MediaReferences"/>, and
    /// records what was kept on the message. A picture that cannot be fetched is counted as skipped rather than
    /// failing the message, so the text of the message is never lost to a broken image.
    /// </summary>
    /// <param name="message">The received message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the message was changed and needs saving.</returns>
    Task<bool> IngestAsync(OmnichannelMessage message, CancellationToken cancellationToken = default);
}

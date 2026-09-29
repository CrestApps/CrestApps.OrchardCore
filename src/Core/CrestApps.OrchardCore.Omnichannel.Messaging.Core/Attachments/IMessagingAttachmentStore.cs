namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Keeps the pictures exchanged in messaging conversations, encrypted at rest and out of the public media library,
/// so a customer's photo is only ever served to someone allowed to read the conversation it belongs to.
/// </summary>
public interface IMessagingAttachmentStore
{
    /// <summary>
    /// Stores a picture under the specified key, replacing whatever was stored under it, so storing the same
    /// received picture twice keeps one copy.
    /// </summary>
    /// <param name="attachmentId">The key to store the picture under.</param>
    /// <param name="content">The picture's bytes.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StoreAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a stored picture back.
    /// </summary>
    /// <param name="attachmentId">The key the picture was stored under.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The picture's bytes, or <see langword="null"/> when nothing is stored under the key.</returns>
    Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored picture. Deleting one that is already gone succeeds.
    /// </summary>
    /// <param name="attachmentId">The key the picture was stored under.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when nothing remains stored under the key.</returns>
    Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default);
}

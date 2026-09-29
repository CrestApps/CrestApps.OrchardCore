namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Builds the address a provider downloads an outbound picture from. A picture message carries links, not bytes,
/// so the provider must be able to fetch the picture without signing in; the link is signed and expires, and it
/// names nothing but the picture.
/// </summary>
public interface IMessagingAttachmentUrlProvider
{
    /// <summary>
    /// Builds the public, time-limited address of a stored picture.
    /// </summary>
    /// <param name="attachment">The stored picture.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The absolute address, or <see langword="null"/> when the site has no public address to build it from.
    /// </returns>
    Task<string> GetPublicUrlAsync(MessagingAttachment attachment, CancellationToken cancellationToken = default);
}

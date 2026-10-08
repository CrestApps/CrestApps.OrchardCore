namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Signs the download of a received picture for the provider that hosts it. Some providers serve inbound media only
/// to a caller holding the account's credentials; the channel feature that knows the provider registers one of these
/// so the workspace can copy the picture without knowing whose it is.
/// </summary>
public interface IMessagingMediaRequestAuthenticator
{
    /// <summary>
    /// Adds the provider's credentials to a download request, when the address is one this provider hosts.
    /// </summary>
    /// <param name="request">The download request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the request was for this provider and has been signed.</returns>
    Task<bool> TryAuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

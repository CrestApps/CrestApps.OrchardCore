namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Creates and reads the signed unsubscribe links bulk and outreach email carries. A link names the address it was sent
/// to and cannot be altered to unsubscribe anyone else.
/// </summary>
public interface IEmailUnsubscribeLinks
{
    /// <summary>
    /// Creates the unsubscribe link for an email sent to a contact address.
    /// </summary>
    /// <param name="contactAddress">The address the email is sent to.</param>
    /// <param name="serviceAddress">Our address the email is sent from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The absolute link, or <see langword="null"/> when the site has no public base URL to build it on.</returns>
    Task<string> CreateUrlAsync(string contactAddress, string serviceAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the token of an unsubscribe link.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <param name="contactAddress">The address the email was sent to.</param>
    /// <param name="serviceAddress">Our address the email was sent from.</param>
    /// <returns><see langword="true"/> when the token is genuine.</returns>
    bool TryRead(string token, out string contactAddress, out string serviceAddress);
}

namespace CrestApps.OrchardCore.Consent.Unsubscribe;

/// <summary>
/// Creates and reads the signed unsubscribe links every marketing e-mail carries. A link opens the site's one e-mail
/// preferences page, which offers the link's scope and "stop all marketing e-mail", and also accepts the RFC 8058
/// one-click <c>POST</c>.
/// </summary>
public interface IUnsubscribeLinks
{
    /// <summary>
    /// Creates the absolute unsubscribe URL for an address and a scope, built on the site's base URL.
    /// </summary>
    /// <param name="address">The e-mail address the message goes to.</param>
    /// <param name="scope">What the link stops.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The URL, or <see langword="null"/> when the address is empty or the site has no base URL.</returns>
    Task<string> CreateUrlAsync(string address, UnsubscribeScope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a token taken from an unsubscribe URL.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ticket">The address and scope the token holds, when it is valid.</param>
    /// <returns><see langword="true"/> when the token was made by this tenant for unsubscribing and is not expired.</returns>
    bool TryRead(string token, out UnsubscribeTicket ticket);
}

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Records which browser soft-phone credential an authenticated user's client is actually registered on.
/// </summary>
/// <remarks>
/// A provider that mints short-lived browser SIP credentials can have several live at once for one user: a
/// renewal mints a fresh credential before the predecessor expires, and a registration that does not complete
/// leaves its credential live but unusable. The client is registered on exactly one of them, and issuance
/// recency is not a reliable proxy for which -- a credential minted last can be one no client ever registered
/// on. Delivering a call to such a credential is refused by the provider (Telnyx answers SIP 486), so the
/// client reports the credential it registered on and the platform delivers to that one.
/// </remarks>
public interface ISoftPhoneCredentialRegistrar
{
    /// <summary>
    /// Gets the technical provider name handled by this registrar.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Records that the user's client completed registration on the specified credential. Implementations only
    /// act on a credential they own for that user, so a provider handed another provider's identifier simply
    /// returns without doing anything.
    /// </summary>
    /// <param name="userId">The authenticated user the credential must belong to.</param>
    /// <param name="credentialId">The provider credential identifier the client registered on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a credential owned by the user was marked registered; otherwise <see langword="false"/>.</returns>
    Task<bool> ReportRegisteredAsync(
        string userId,
        string credentialId,
        CancellationToken cancellationToken = default);
}

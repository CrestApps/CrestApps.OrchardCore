using CrestApps.OrchardCore.Telnyx.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Orders a user's live browser SIP credentials by how likely each is to actually receive a call.
/// </summary>
public static class TelnyxAgentCredentialSelection
{
    /// <summary>
    /// Orders live credentials best delivery target first.
    /// </summary>
    /// <remarks>
    /// Several credentials can be live for one user at once: a renewal mints a fresh credential before its
    /// predecessor expires, and a registration that never completes leaves its credential live but unusable.
    /// The client is registered on exactly one of them, so issuance recency is not a proxy for reachability --
    /// the newest-issued credential can be one no client ever registered on, and Telnyx refuses delivery to it
    /// with SIP 486, leaving the agent's leg silently unreachable. A credential the client reported registering
    /// on therefore wins, most recently registered first. Credentials that were never reported fall back to
    /// newest-issued, so a client that predates the report still resolves to something.
    /// </remarks>
    /// <param name="credentials">The live credentials to order.</param>
    /// <returns>The credentials ordered best delivery target first.</returns>
    public static IReadOnlyList<TelnyxAgentCredential> OrderByDeliveryPreference(IEnumerable<TelnyxAgentCredential> credentials)
    {
        if (credentials is null)
        {
            return [];
        }

        return credentials
            .OrderByDescending(credential => credential.RegisteredUtc.HasValue)
            .ThenByDescending(credential => credential.RegisteredUtc ?? DateTime.MinValue)
            .ThenByDescending(credential => credential.IssuedUtc)
            .ToList();
    }
}

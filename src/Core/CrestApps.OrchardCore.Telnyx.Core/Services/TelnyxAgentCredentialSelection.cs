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
    /// <para>
    /// One user can also have the soft phone open in several windows, each registered on a credential of its own.
    /// A credential whose window's connection has closed ranks after every credential registered by a window still
    /// open, so closing the window that registered last hands calls to one that is still there. It still ranks ahead
    /// of a credential nothing registered on: a window that only lost its connection for a moment stays registered
    /// with the provider.
    /// </para>
    /// </remarks>
    /// <param name="credentials">The live credentials to order.</param>
    /// <returns>The credentials ordered best delivery target first.</returns>
    public static IReadOnlyList<TelnyxAgentCredential> OrderByDeliveryPreference(IEnumerable<TelnyxAgentCredential> credentials)
    {
        if (credentials is null)
        {
            return [];
        }

        // A credential a leg was refused on, or that its phone moved off, is the last resort whatever else it says.
        return credentials
            .OrderBy(credential => credential.UnreachableUtc.HasValue)
            .ThenByDescending(IsRegisteredByAnOpenWindow)
            .ThenByDescending(credential => credential.RegisteredUtc.HasValue)
            .ThenByDescending(credential => credential.RegisteredUtc ?? DateTime.MinValue)
            .ThenByDescending(credential => credential.IssuedUtc)
            .ToList();
    }

    /// <summary>
    /// Orders the credentials a leg that was just refused as unavailable may be rung on instead, best first.
    /// </summary>
    /// <remarks>
    /// The refusal is evidence that the phone the store believed in is gone, which is what a phone reopening looks like:
    /// its old window closed, and it minted a fresh credential it is registering on right now. So a credential a still
    /// open window registered on stays first, but after it the phone's most recent sign of life wins -- when a closed
    /// window was last seen, or when a credential was minted -- rather than a closed window always beating a
    /// credential nothing has registered on yet. Credentials known to be unreachable are left out: ringing one again
    /// only repeats the refusal.
    /// </remarks>
    /// <param name="credentials">The live credentials to choose from.</param>
    /// <returns>The credentials still worth trying, best delivery target first.</returns>
    public static IReadOnlyList<TelnyxAgentCredential> OrderForRedelivery(IEnumerable<TelnyxAgentCredential> credentials)
    {
        if (credentials is null)
        {
            return [];
        }

        return credentials
            .Where(credential => !credential.UnreachableUtc.HasValue)
            .OrderByDescending(IsRegisteredByAnOpenWindow)
            .ThenByDescending(LastSeenUtc)
            .ThenByDescending(credential => credential.IssuedUtc)
            .ToList();
    }

    private static bool IsRegisteredByAnOpenWindow(TelnyxAgentCredential credential)
        => credential.RegisteredUtc.HasValue && !credential.ConnectionClosedUtc.HasValue;

    private static DateTime LastSeenUtc(TelnyxAgentCredential credential)
        => IsRegisteredByAnOpenWindow(credential)
            ? credential.RegisteredUtc.Value
            : credential.ConnectionClosedUtc ?? credential.RegisteredUtc ?? credential.IssuedUtc;
}

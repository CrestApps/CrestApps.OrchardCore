using CrestApps.OrchardCore.Telnyx.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Persists and queries the durable mapping between an authenticated user and the browser SIP credential
/// minted for their Telnyx soft phone.
/// </summary>
public interface ITelnyxAgentCredentialStore
{
    /// <summary>
    /// Persists a newly issued credential record.
    /// </summary>
    Task CreateAsync(TelnyxAgentCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the live (not revoked, not expired) credentials owned by a user, newest first.
    /// </summary>
    Task<IReadOnlyList<TelnyxAgentCredential>> ListLiveByUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every credential owned by a user, regardless of state.
    /// </summary>
    Task<IReadOnlyList<TelnyxAgentCredential>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a credential as revoked.
    /// </summary>
    Task MarkRevokedAsync(TelnyxAgentCredential credential, DateTime revokedUtc, CancellationToken cancellationToken = default);
}

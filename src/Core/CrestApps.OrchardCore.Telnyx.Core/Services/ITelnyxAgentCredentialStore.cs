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
    /// Lists the live (not revoked, not expired) credentials owned by a user, best delivery target first: the
    /// credential a client whose connection is still open most recently reported registering on, then those whose
    /// connection has closed, then any credential that has never been reported as registered, newest issued first.
    /// </summary>
    Task<IReadOnlyList<TelnyxAgentCredential>> ListLiveByUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every credential owned by a user, regardless of state.
    /// </summary>
    Task<IReadOnlyList<TelnyxAgentCredential>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the user's client completed SIP registration on a credential.
    /// </summary>
    Task<bool> MarkRegisteredAsync(string userId, string credentialId, DateTime registeredUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the user's client completed SIP registration on a credential, through the soft-phone connection
    /// that reported it.
    /// </summary>
    Task<bool> MarkRegisteredAsync(string userId, string credentialId, string connectionId, DateTime registeredUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a soft-phone connection closed, against the user's live credentials it had registered on.
    /// </summary>
    /// <returns>How many credentials the connection had registered on.</returns>
    Task<int> MarkConnectionClosedAsync(string userId, string connectionId, DateTime closedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records what the client registered on a user's credential reported it can do, replacing what it reported
    /// before.
    /// </summary>
    Task<bool> SetClientCapabilitiesAsync(string userId, string credentialId, IReadOnlyCollection<string> capabilities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a credential as revoked.
    /// </summary>
    Task MarkRevokedAsync(TelnyxAgentCredential credential, DateTime revokedUtc, CancellationToken cancellationToken = default);
}

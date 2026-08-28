using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Models;
using OrchardCore.Environment.Shell;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Provides the default YesSql-backed implementation of <see cref="ITelnyxAgentCredentialStore"/>.
/// </summary>
public sealed class TelnyxAgentCredentialStore : ITelnyxAgentCredentialStore
{
    private readonly ISession _session;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAgentCredentialStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="shellSettings">The current tenant shell settings.</param>
    public TelnyxAgentCredentialStore(ISession session, ShellSettings shellSettings)
    {
        _session = session;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(TelnyxAgentCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        credential.TenantName = GetTenantName();
        await _session.SaveAsync(credential, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelnyxAgentCredential>> ListLiveByUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var tenantName = GetTenantName();
        var normalizedUserId = userId.Trim();

        var credentials = await _session
            .Query<TelnyxAgentCredential, TelnyxAgentCredentialIndex>(index =>
                index.TenantName == tenantName &&
                index.UserId == normalizedUserId &&
                !index.Revoked &&
                index.ExpiresUtc > nowUtc)
            .ListAsync(cancellationToken);

        // Order by what actually makes a credential reachable, not by when it was minted. Several credentials
        // can be live for one user at once -- a renewal mints a fresh one before its predecessor expires, and a
        // registration that never completes leaves its credential live but unusable -- and the client is
        // registered on exactly one of them. Delivering a call to a credential no client registered on is
        // refused by Telnyx with SIP 486, which is why the newest-issued credential is the wrong choice. A
        // credential the client reported registering on wins, most recently registered first; credentials that
        // were never reported fall back to newest-issued so a client that predates the report still works.
        return TelnyxAgentCredentialSelection.OrderByDeliveryPreference(credentials);
    }

    /// <inheritdoc/>
    public async Task<bool> MarkRegisteredAsync(string userId, string credentialId, DateTime registeredUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(credentialId))
        {
            return false;
        }

        var tenantName = GetTenantName();
        var normalizedUserId = userId.Trim();
        var normalizedCredentialId = credentialId.Trim();

        // Scoped to the caller's own credentials so a client cannot mark someone else's credential registered.
        var credential = await _session
            .Query<TelnyxAgentCredential, TelnyxAgentCredentialIndex>(index =>
                index.TenantName == tenantName &&
                index.UserId == normalizedUserId &&
                index.CredentialId == normalizedCredentialId)
            .FirstOrDefaultAsync();

        if (credential is null)
        {
            return false;
        }

        credential.RegisteredUtc = registeredUtc;

        await _session.SaveAsync(credential, cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelnyxAgentCredential>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var tenantName = GetTenantName();
        var normalizedUserId = userId.Trim();

        var credentials = await _session
            .Query<TelnyxAgentCredential, TelnyxAgentCredentialIndex>(index =>
                index.TenantName == tenantName &&
                index.UserId == normalizedUserId)
            .ListAsync(cancellationToken);

        return credentials.ToList();
    }

    /// <inheritdoc/>
    public async Task MarkRevokedAsync(TelnyxAgentCredential credential, DateTime revokedUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        credential.RevokedUtc = revokedUtc;
        await _session.SaveAsync(credential, cancellationToken: cancellationToken);
    }

    private string GetTenantName()
        => string.IsNullOrWhiteSpace(_shellSettings.Name) ? "Default" : _shellSettings.Name.Trim();
}

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
    public Task<bool> MarkRegisteredAsync(string userId, string credentialId, DateTime registeredUtc, CancellationToken cancellationToken = default)
        => MarkRegisteredAsync(userId, credentialId, connectionId: null, registeredUtc, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> MarkRegisteredAsync(string userId, string credentialId, string connectionId, DateTime registeredUtc, CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (credential is null)
        {
            return false;
        }

        credential.RegisteredUtc = registeredUtc;

        // Registered again is live again, whichever connection says so.
        credential.RegisteredConnectionId = string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        credential.ConnectionClosedUtc = null;
        credential.UnreachableUtc = null;

        await _session.SaveAsync(credential, cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            await MarkSupersededAsync(normalizedUserId, credential, connectionId, registeredUtc, cancellationToken);
        }

        return true;
    }

    // One connection's phone is registered on one credential at a time. When it registers on another, the credential it
    // left still reads as registered by an open connection, and would keep being rung until it expired.
    private async Task MarkSupersededAsync(
        string userId,
        TelnyxAgentCredential registered,
        string connectionId,
        DateTime registeredUtc,
        CancellationToken cancellationToken)
    {
        foreach (var credential in await ListLiveByUserAsync(userId, registeredUtc, cancellationToken))
        {
            if (credential.Id == registered.Id ||
                string.Equals(credential.CredentialId, registered.CredentialId, StringComparison.Ordinal) ||
                credential.UnreachableUtc.HasValue ||
                !string.Equals(credential.RegisteredConnectionId, connectionId, StringComparison.Ordinal))
            {
                continue;
            }

            credential.UnreachableUtc = registeredUtc;
            await _session.SaveAsync(credential, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> MarkConnectionClosedAsync(string userId, string connectionId, DateTime closedUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
        {
            return 0;
        }

        var marked = 0;

        // Only the user's live credentials: an expired or revoked one is never delivered to anyway.
        foreach (var credential in await ListLiveByUserAsync(userId, closedUtc, cancellationToken))
        {
            if (!string.Equals(credential.RegisteredConnectionId, connectionId, StringComparison.Ordinal) ||
                credential.ConnectionClosedUtc.HasValue)
            {
                continue;
            }

            credential.ConnectionClosedUtc = closedUtc;
            await _session.SaveAsync(credential, cancellationToken: cancellationToken);
            marked++;
        }

        return marked;
    }

    /// <inheritdoc/>
    public async Task<TelnyxAgentCredential> MarkUnreachableAsync(string userId, string sipUsername, DateTime unreachableUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sipUsername))
        {
            return null;
        }

        var normalizedSipUsername = sipUsername.Trim();

        // Only the user's own live credentials: a leg to someone else's address says nothing about this user's phone.
        var credential = (await ListLiveByUserAsync(userId, unreachableUtc, cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.SipUsername, normalizedSipUsername, StringComparison.Ordinal));

        if (credential is null)
        {
            return null;
        }

        credential.UnreachableUtc ??= unreachableUtc;
        await _session.SaveAsync(credential, cancellationToken: cancellationToken);

        return credential;
    }

    /// <inheritdoc/>
    public async Task<bool> SetClientCapabilitiesAsync(
        string userId,
        string credentialId,
        IReadOnlyCollection<string> capabilities,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(credentialId))
        {
            return false;
        }

        var tenantName = GetTenantName();
        var normalizedUserId = userId.Trim();
        var normalizedCredentialId = credentialId.Trim();

        // Scoped to the caller's own credentials, like the registration report.
        var credential = await _session
            .Query<TelnyxAgentCredential, TelnyxAgentCredentialIndex>(index =>
                index.TenantName == tenantName &&
                index.UserId == normalizedUserId &&
                index.CredentialId == normalizedCredentialId)
            .FirstOrDefaultAsync(cancellationToken);

        if (credential is null)
        {
            return false;
        }

        credential.ClientCapabilities = capabilities is null ? [] : [.. capabilities];

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

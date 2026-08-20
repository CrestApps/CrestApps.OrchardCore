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

        return credentials
            .OrderByDescending(credential => credential.IssuedUtc)
            .ToList();
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

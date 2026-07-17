using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Models;
using YesSql;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="IAsteriskPjsipCredentialLeaseStore"/>. Every mutating
/// operation commits in its OWN isolated session created from the tenant <see cref="IStore"/>, so the lease
/// becomes durable immediately — independent of the ambient request scope and before the tenant distributed
/// lock is released. Because all sessions are opened from the tenant store, operations are inherently
/// isolated to the current tenant and never observe or mutate another tenant's leases.
/// </summary>
internal sealed class AsteriskPjsipCredentialLeaseStore : IAsteriskPjsipCredentialLeaseStore
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskPjsipCredentialLeaseStore"/> class.
    /// </summary>
    /// <param name="store">The tenant YesSql store used to open isolated, immediately committed sessions.</param>
    public AsteriskPjsipCredentialLeaseStore(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        await using var session = _store.CreateSession();
        session.Save(lease);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        await using var session = _store.CreateSession();

        // Re-materialize the lease inside this session so the update targets a tracked instance and commits
        // durably, even when the caller holds a detached copy created by an earlier isolated session.
        var tracked = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.AuthorizationUser == lease.AuthorizationUser)
            .FirstOrDefaultAsync(cancellationToken);

        if (tracked is null)
        {
            session.Save(lease);
        }
        else
        {
            tracked.TenantName = lease.TenantName;
            tracked.UserId = lease.UserId;
            tracked.SessionId = lease.SessionId;
            tracked.InteractionId = lease.InteractionId;
            tracked.IssuedUtc = lease.IssuedUtc;
            tracked.ExpiresUtc = lease.ExpiresUtc;
            tracked.RevokedUtc = lease.RevokedUtc;
            tracked.RevocationReason = lease.RevocationReason;
            session.Save(tracked);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        await using var session = _store.CreateSession();

        // Delete by the stable authorization-user key inside this session; an entity tracked by a different
        // session cannot be deleted directly.
        var tracked = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.AuthorizationUser == lease.AuthorizationUser)
            .FirstOrDefaultAsync(cancellationToken);

        if (tracked is null)
        {
            return;
        }

        session.Delete(tracked);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskPjsipCredentialLease> GetByAuthorizationUserAsync(
        string authorizationUser,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(authorizationUser))
        {
            return null;
        }

        await using var session = _store.CreateSession();

        return await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.AuthorizationUser == authorizationUser)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveByUserAsync(
        string userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        await using var session = _store.CreateSession();
        var leases = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.UserId == userId &&
                !index.Revoked &&
                index.ExpiresUtc > nowUtc)
            .ListAsync(cancellationToken);

        return leases.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        await using var session = _store.CreateSession();
        var leases = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.UserId == userId)
            .ListAsync(cancellationToken);

        return leases.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveBySessionAsync(
        string sessionId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return [];
        }

        await using var session = _store.CreateSession();
        var leases = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.SessionId == sessionId &&
                !index.Revoked &&
                index.ExpiresUtc > nowUtc)
            .ListAsync(cancellationToken);

        return leases.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListExpiredOrRevokedAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 200 : maxCount;

        await using var session = _store.CreateSession();
        var leases = await session
            .Query<AsteriskPjsipCredentialLease, AsteriskPjsipCredentialLeaseIndex>(index =>
                index.Revoked ||
                index.ExpiresUtc <= nowUtc)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return leases.ToList();
    }
}

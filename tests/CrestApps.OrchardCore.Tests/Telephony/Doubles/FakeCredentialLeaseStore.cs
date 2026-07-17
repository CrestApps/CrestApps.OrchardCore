using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A deterministic, thread-safe in-memory <see cref="IAsteriskPjsipCredentialLeaseStore"/> that stands in
/// for the per-tenant YesSql lease store. Each instance represents a single tenant's isolated store, so
/// pairing separate instances with a shared realtime store lets tests prove that one tenant's cleanup can
/// never observe or delete another tenant's leases.
/// </summary>
internal sealed class FakeCredentialLeaseStore : IAsteriskPjsipCredentialLeaseStore
{
    private readonly object _gate = new();
    private readonly List<AsteriskPjsipCredentialLease> _leases = [];

    /// <summary>
    /// Returns a point-in-time snapshot of every lease held by this store.
    /// </summary>
    /// <returns>A copy of the stored leases.</returns>
    public IReadOnlyList<AsteriskPjsipCredentialLease> Snapshot()
    {
        lock (_gate)
        {
            return _leases.ToList();
        }
    }

    public Task CreateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _leases.Add(lease);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(
        AsteriskPjsipCredentialLease lease,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _leases.RemoveAll(item =>
                ReferenceEquals(item, lease) ||
                string.Equals(item.AuthorizationUser, lease.AuthorizationUser, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }

    public Task<AsteriskPjsipCredentialLease> GetByAuthorizationUserAsync(
        string authorizationUser,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_leases.FirstOrDefault(item =>
                string.Equals(item.AuthorizationUser, authorizationUser, StringComparison.Ordinal)));
        }
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveByUserAsync(
        string userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AsteriskPjsipCredentialLease> matches = _leases
                .Where(item =>
                    string.Equals(item.UserId, userId, StringComparison.Ordinal) &&
                    item.RevokedUtc is null &&
                    item.ExpiresUtc > nowUtc)
                .ToList();

            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AsteriskPjsipCredentialLease> matches = _leases
                .Where(item => string.Equals(item.UserId, userId, StringComparison.Ordinal))
                .ToList();

            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveBySessionAsync(
        string sessionId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AsteriskPjsipCredentialLease> matches = _leases
                .Where(item =>
                    string.Equals(item.SessionId, sessionId, StringComparison.Ordinal) &&
                    item.RevokedUtc is null &&
                    item.ExpiresUtc > nowUtc)
                .ToList();

            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListExpiredOrRevokedAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AsteriskPjsipCredentialLease> matches = _leases
                .Where(item => item.RevokedUtc is not null || item.ExpiresUtc <= nowUtc)
                .OrderBy(item => item.ExpiresUtc)
                .Take(maxCount <= 0 ? int.MaxValue : maxCount)
                .ToList();

            return Task.FromResult(matches);
        }
    }
}

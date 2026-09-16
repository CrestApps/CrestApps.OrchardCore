using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

internal sealed class FakeAsteriskPjsipCredentialLeaseStore : IAsteriskPjsipCredentialLeaseStore
{
    private readonly List<AsteriskPjsipCredentialLease> _liveLeases;

    public FakeAsteriskPjsipCredentialLeaseStore(params AsteriskPjsipCredentialLease[] liveLeases)
    {
        _liveLeases = [.. liveLeases];
    }

    public string LastListLiveUserId { get; private set; }

    public Task CreateAsync(AsteriskPjsipCredentialLease lease, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AsteriskPjsipCredentialLease lease, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AsteriskPjsipCredentialLease lease, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<AsteriskPjsipCredentialLease> GetByAuthorizationUserAsync(string authorizationUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AsteriskPjsipCredentialLease>(null);
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveByUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        LastListLiveUserId = userId;

        var matches = _liveLeases
            .Where(lease => string.Equals(lease.UserId, userId, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<AsteriskPjsipCredentialLease>>(matches);
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AsteriskPjsipCredentialLease>>([]);
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListLiveBySessionAsync(string sessionId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AsteriskPjsipCredentialLease>>([]);
    }

    public Task<IReadOnlyList<AsteriskPjsipCredentialLease>> ListExpiredOrRevokedAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AsteriskPjsipCredentialLease>>([]);
    }
}

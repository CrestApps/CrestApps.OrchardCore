using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskPjsipCredentialLeaseStoreTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LeaseStore_PersistsAndQueriesLeasesThroughTheYesSqlIndex()
    {
        var databasePath = LeaseStoreTestHarness.DatabasePath("lease-index");
        var store = await LeaseStoreTestHarness.CreateStoreAsync(databasePath, TestContext.Current.CancellationToken);

        try
        {
            var live = NewLease("cc-acme-live", "user-1", "session-live", _now.AddMinutes(15), revokedUtc: null);
            var expired = NewLease("cc-acme-expired", "user-1", "session-expired", _now.AddMinutes(-5), revokedUtc: null);
            var revoked = NewLease("cc-acme-revoked", "user-2", "session-revoked", _now.AddMinutes(15), revokedUtc: _now.AddMinutes(-1));

            await SeedAsync(store, live, expired, revoked);

            var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);
            var cancellationToken = TestContext.Current.CancellationToken;

            // Exact-authorization-user lookup.
            var byAuth = await leaseStore.GetByAuthorizationUserAsync("cc-acme-live", cancellationToken);
            Assert.NotNull(byAuth);
            Assert.Equal("user-1", byAuth.UserId);

            // Live-by-user excludes the expired lease.
            var liveByUser = await leaseStore.ListLiveByUserAsync("user-1", _now, cancellationToken);
            Assert.Single(liveByUser);
            Assert.Equal("cc-acme-live", liveByUser[0].AuthorizationUser);

            // All-by-user includes the expired lease.
            var allByUser = await leaseStore.ListByUserAsync("user-1", cancellationToken);
            Assert.Equal(2, allByUser.Count);

            // Live-by-session resolves the exact session.
            var liveBySession = await leaseStore.ListLiveBySessionAsync("session-live", _now, cancellationToken);
            Assert.Single(liveBySession);
            Assert.Equal("cc-acme-live", liveBySession[0].AuthorizationUser);

            // Cleanup candidates are the expired and revoked leases only.
            var cleanup = await leaseStore.ListExpiredOrRevokedAsync(_now, 10, cancellationToken);
            var cleanupUsers = cleanup.Select(lease => lease.AuthorizationUser).ToArray();
            Assert.Equal(2, cleanupUsers.Length);
            Assert.Contains("cc-acme-expired", cleanupUsers);
            Assert.Contains("cc-acme-revoked", cleanupUsers);
            Assert.DoesNotContain("cc-acme-live", cleanupUsers);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task LeaseStore_DeleteRemovesLeaseFromTheIndex()
    {
        var databasePath = LeaseStoreTestHarness.DatabasePath("lease-delete");
        var store = await LeaseStoreTestHarness.CreateStoreAsync(databasePath, TestContext.Current.CancellationToken);

        try
        {
            var lease = NewLease("cc-acme-delete", "user-9", "session-9", _now.AddMinutes(15), revokedUtc: null);
            await SeedAsync(store, lease);

            var cancellationToken = TestContext.Current.CancellationToken;
            var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);

            var tracked = await leaseStore.GetByAuthorizationUserAsync("cc-acme-delete", cancellationToken);
            await leaseStore.DeleteAsync(tracked, cancellationToken);

            var reloaded = await leaseStore.GetByAuthorizationUserAsync("cc-acme-delete", cancellationToken);
            Assert.Null(reloaded);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static AsteriskPjsipCredentialLease NewLease(
        string authorizationUser,
        string userId,
        string sessionId,
        DateTime expiresUtc,
        DateTime? revokedUtc)
        => new()
        {
            AuthorizationUser = authorizationUser,
            TenantName = "acme",
            UserId = userId,
            SessionId = sessionId,
            InteractionId = null,
            IssuedUtc = _now,
            ExpiresUtc = expiresUtc,
            RevokedUtc = revokedUtc,
            RevocationReason = revokedUtc is null ? null : "revoked",
        };

    private static async Task SeedAsync(IStore store, params AsteriskPjsipCredentialLease[] leases)
    {
        var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);

        foreach (var lease in leases)
        {
            await leaseStore.CreateAsync(lease, TestContext.Current.CancellationToken);
        }
    }
}

using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Durability tests that wire <see cref="AsteriskPjsipCredentialIssuer"/> to the REAL
/// <see cref="AsteriskPjsipCredentialLeaseStore"/> over an in-memory YesSql <see cref="IStore"/>. These
/// prove the lease is committed durably (visible to a separate session) BEFORE the realtime row is
/// provisioned and BEFORE the tenant lock releases, which the in-memory fake cannot demonstrate. They fail
/// against the old <c>SaveAsync</c>-only (ambient-scope) lease store.
/// </summary>
public sealed class AsteriskPjsipCredentialIssuerDurabilityTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IssueAsync_CommitsLeaseDurablyBeforeRealtimeProvisioning_AndCleanupReclaimsOnFailure()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = LeaseStoreTestHarness.DatabasePath("issuer-orphan");
        var store = await LeaseStoreTestHarness.CreateStoreAsync(databasePath, cancellationToken);

        try
        {
            var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);
            var realtimeStore = new LeaseAssertingRealtimeStore(store, throwOnUpsert: true);
            var issuer = CreateIssuer("TenantA", store, leaseStore, realtimeStore);

            // Act: provisioning throws AFTER the durable lease is committed.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                issuer.IssueAsync(CreateRequest(), cancellationToken));

            // Assert: the lease was already committed and visible from a SEPARATE session at provisioning
            // time (fails on the old SaveAsync-only store, where nothing is committed yet).
            Assert.True(realtimeStore.LeaseVisibleAtUpsert);

            var verifier = new AsteriskPjsipCredentialLeaseStore(store);
            var leases = await verifier.ListByUserAsync("user-1", cancellationToken);
            var lease = Assert.Single(leases);
            Assert.NotNull(lease.RevokedUtc);
            Assert.Equal("provision_failed", lease.RevocationReason);

            // A subsequent cleanup reclaims the realtime row by EXACT authorization user and deletes the lease.
            var removed = await issuer.CleanupExpiredAsync(cancellationToken);
            Assert.Equal(1, removed);
            Assert.Contains(lease.AuthorizationUser, realtimeStore.Deleted);
            Assert.Empty(await verifier.ListByUserAsync("user-1", cancellationToken));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task IssueAsync_EnforcesCapFromCommittedLeases_AfterLockReleases()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = LeaseStoreTestHarness.DatabasePath("issuer-cap");
        var store = await LeaseStoreTestHarness.CreateStoreAsync(databasePath, cancellationToken);

        try
        {
            var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);
            var realtimeStore = new LeaseAssertingRealtimeStore(store, throwOnUpsert: false);
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);
            var issuer = CreateIssuer("TenantA", store, leaseStore, realtimeStore, clock);

            var credentials = new List<AsteriskPjsipCredential>();

            for (var index = 0; index < 3; index++)
            {
                // Advance the clock so the oldest lease is deterministic and each IssueAsync fully returns
                // (releasing the tenant lock) before the next issue.
                clock.SetupGet(value => value.UtcNow).Returns(_now.AddMinutes(index));
                credentials.Add(await issuer.IssueAsync(CreateRequest(userId: "user-capped"), cancellationToken));
            }

            // After the third issue returns, a FRESH session must observe all three committed leases; this is
            // exactly what the cap query depends on (and what the old ambient-scope store could not provide).
            var verifier = new AsteriskPjsipCredentialLeaseStore(store);
            var liveBeforeCap = await verifier.ListLiveByUserAsync("user-capped", _now.AddMinutes(2), cancellationToken);
            Assert.Equal(3, liveBeforeCap.Count);

            // Act: the fourth issue must see the committed leases and revoke the oldest.
            clock.SetupGet(value => value.UtcNow).Returns(_now.AddMinutes(3));
            credentials.Add(await issuer.IssueAsync(CreateRequest(userId: "user-capped"), cancellationToken));

            // Assert
            Assert.Contains(credentials[0].AuthorizationUser, realtimeStore.Deleted);

            var liveAfterCap = await verifier.ListLiveByUserAsync("user-capped", _now.AddMinutes(3), cancellationToken);
            Assert.Equal(3, liveAfterCap.Count);
            Assert.DoesNotContain(liveAfterCap, lease => lease.AuthorizationUser == credentials[0].AuthorizationUser);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RevokeUserAsync_TearsDownDurableLeaseAndRealtime_DespiteCacheOutage()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = LeaseStoreTestHarness.DatabasePath("issuer-cache-outage");
        var store = await LeaseStoreTestHarness.CreateStoreAsync(databasePath, cancellationToken);

        try
        {
            var leaseStore = new AsteriskPjsipCredentialLeaseStore(store);
            var realtimeStore = new LeaseAssertingRealtimeStore(store, throwOnUpsert: false);

            // The cache throws on EVERY operation for the whole lifecycle, so issuance's best-effort cache
            // write and revocation's cache read/remove all fail; only the durable lease keeps the flow correct.
            var issuer = CreateIssuer(
                "TenantA",
                store,
                leaseStore,
                realtimeStore,
                cache: new ThrowingDistributedCache());

            var credential = await issuer.IssueAsync(CreateRequest(userId: "user-outage"), cancellationToken);

            // The durable lease and realtime row exist even though the cache write threw.
            var verifier = new AsteriskPjsipCredentialLeaseStore(store);
            Assert.Single(await verifier.ListByUserAsync("user-outage", cancellationToken));

            // Act: sign-out revocation runs entirely while the cache is down.
            var revoked = await issuer.RevokeUserAsync("user-outage", "signed_out", cancellationToken);

            // Assert: revocation succeeded, the realtime row was torn down by EXACT authorization user, and the
            // durable lease was deleted — none of which depended on the (throwing) cache.
            Assert.Equal(1, revoked);
            Assert.Contains(credential.AuthorizationUser, realtimeStore.Deleted);
            Assert.Empty(await verifier.ListByUserAsync("user-outage", cancellationToken));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static AsteriskPjsipCredentialIssuer CreateIssuer(
        string tenantName,
        IStore store,
        IAsteriskPjsipCredentialLeaseStore leaseStore,
        IAsteriskPjsipRealtimeCredentialStore realtimeStore,
        Mock<IClock> clock = null,
        IDistributedCache cache = null)
    {
        clock ??= new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var tagCache = new Mock<ITagCache>();
        tagCache.Setup(cache => cache.RemoveTagAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        return new AsteriskPjsipCredentialIssuer(
            cache ?? new FakeDistributedCache(),
            tagCache.Object,
            new FakeDistributedLock(),
            clock.Object,
            new ShellSettings { Name = tenantName },
            realtimeStore,
            leaseStore,
            new StubDialogTerminator());
    }

    private static AsteriskPjsipCredentialIssueRequest CreateRequest(
        string userId = "user-1")
        => new()
        {
            UserId = userId,
            DisplayName = "Agent One",
            SessionId = null,
            InteractionId = null,
            SipDomain = "pbx.example.test",
            CredentialLifetime = TimeSpan.FromMinutes(15),
            ContactExpiration = TimeSpan.FromSeconds(120),
            Codecs = ["opus", "g722", "ulaw"],
        };

    private sealed class LeaseAssertingRealtimeStore : IAsteriskPjsipRealtimeCredentialStore
    {
        private readonly IStore _store;
        private readonly bool _throwOnUpsert;

        public LeaseAssertingRealtimeStore(IStore store, bool throwOnUpsert)
        {
            _store = store;
            _throwOnUpsert = throwOnUpsert;
        }

        public List<string> Deleted { get; } = [];

        public bool LeaseVisibleAtUpsert { get; private set; }

        public async Task UpsertAsync(
            AsteriskPjsipRealtimeCredential credential,
            CancellationToken cancellationToken = default)
        {
            // Probe a SEPARATE fresh session to confirm the durable lease is already committed BEFORE the
            // realtime row is written. On the old ambient-scope store the lease is still uncommitted here.
            var probe = new AsteriskPjsipCredentialLeaseStore(_store);
            var lease = await probe.GetByAuthorizationUserAsync(credential.AuthorizationUser, cancellationToken);
            LeaseVisibleAtUpsert = lease is not null;

            if (_throwOnUpsert)
            {
                throw new InvalidOperationException("Realtime provisioning failed.");
            }
        }

        public Task DeleteAsync(
            string authorizationUser,
            CancellationToken cancellationToken = default)
        {
            Deleted.Add(authorizationUser);

            return Task.CompletedTask;
        }
    }

    private sealed class StubDialogTerminator : IAsteriskPjsipDialogTerminator
    {
        public Task TerminateAsync(
            string authorizationUser,
            string reason,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

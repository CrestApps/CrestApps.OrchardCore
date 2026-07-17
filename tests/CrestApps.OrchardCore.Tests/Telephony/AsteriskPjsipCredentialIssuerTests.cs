using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskPjsipCredentialIssuerTests
{
    [Fact]
    public async Task IssueAsync_CreatesTenantNamespacedRealtimeCredentialWithoutCachingPlaintext()
    {
        // Arrange
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", store: store);

        // Act
        var credential = await issuer.IssueAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("cc-tenanta-", credential.AuthorizationUser, StringComparison.Ordinal);
        Assert.Equal("sip:" + credential.AuthorizationUser + "@pbx.example.test", credential.SipUri);
        Assert.NotEmpty(credential.Password);
        Assert.Single(store.Upserted);
        Assert.Equal(credential.Password, store.Upserted[0].Password);
        Assert.DoesNotContain(credential.Password, await issuer.Cache.GetStringAsync(issuer.RecordKey(credential.AuthorizationUser), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RotateAsync_RevokesExistingSessionAndIssuesNewCredential()
    {
        // Arrange
        var store = new TestRealtimeCredentialStore();
        var terminator = new TestDialogTerminator();
        var issuer = CreateIssuer("TenantA", store: store, terminator: terminator);
        var first = await issuer.IssueAsync(CreateRequest(sessionId: "interaction-1"), TestContext.Current.CancellationToken);

        // Act
        var second = await issuer.RotateAsync(CreateRequest(sessionId: "interaction-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(first.AuthorizationUser, second.AuthorizationUser);
        Assert.Contains(first.AuthorizationUser, store.Deleted);
        Assert.Contains(terminator.Terminated, item => item.AuthorizationUser == first.AuthorizationUser && item.Reason == "rotated");
        Assert.Equal("interaction-1", second.SessionId);
    }

    [Fact]
    public async Task CleanupExpiredAsync_DeletesExpiredCredentialsOnlyForCurrentTenant()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var clock = new Mock<IClock>();
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(value => value.UtcNow).Returns(now);
        var tenantAStore = new TestRealtimeCredentialStore();
        var tenantAIssuer = CreateIssuer("TenantA", cache, clock, tenantAStore);
        var tenantBIssuer = CreateIssuer("TenantB", cache, clock, new TestRealtimeCredentialStore());
        var expired = await tenantAIssuer.IssueAsync(CreateRequest(lifetime: TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await tenantBIssuer.IssueAsync(CreateRequest(lifetime: TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        clock.SetupGet(value => value.UtcNow).Returns(now.AddMinutes(2));

        // Act
        var removed = await tenantAIssuer.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, removed);
        Assert.Contains(expired.AuthorizationUser, tenantAStore.Deleted);
    }

    [Fact]
    public async Task CleanupExpiredAsync_DoesNotDeleteLiveCredentialWhenOnlyCacheRecordEvicted()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", cache, store: store);
        var credential = await issuer.IssueAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Simulate the distributed cache evicting the credential record while the durable lease is still
        // live. Under lease authority a cache miss must never be interpreted as expiry.
        await cache.RemoveAsync(issuer.RecordKey(credential.AuthorizationUser), TestContext.Current.CancellationToken);

        // Act
        var removed = await issuer.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, removed);
        Assert.DoesNotContain(credential.AuthorizationUser, store.Deleted);
        Assert.Single(issuer.LiveLeases());
    }

    [Fact]
    public async Task CleanupExpiredAsync_DeletesRealtimeRowByExactIdWhenLeaseExpired()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var clock = new Mock<IClock>();
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(value => value.UtcNow).Returns(now);
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", cache, clock, store);
        var credential = await issuer.IssueAsync(CreateRequest(lifetime: TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        // Evict the cache so the only remaining source of expiry is the durable lease.
        cache.Clear();
        clock.SetupGet(value => value.UtcNow).Returns(now.AddMinutes(2));

        // Act
        var removed = await issuer.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal([credential.AuthorizationUser], store.Deleted);
        Assert.Empty(issuer.LiveLeases());
    }

    [Fact]
    public async Task CleanupExpiredAsync_DoesNotDeleteAnotherTenantsRowsWhenSanitizedPrefixesCollide()
    {
        // Arrange: two tenants whose sanitized prefixes collide ("cc-acme-" is a prefix of
        // "cc-acme-east-...") share ONE realtime store but have separate, tenant-isolated lease stores.
        var cache = new FakeDistributedCache();
        var clock = new Mock<IClock>();
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(value => value.UtcNow).Returns(now);
        var sharedRealtimeStore = new TestRealtimeCredentialStore();
        var acmeIssuer = CreateIssuer("acme", cache, clock, sharedRealtimeStore, leaseStore: new FakeCredentialLeaseStore());
        var acmeEastIssuer = CreateIssuer("acme-east", cache, clock, sharedRealtimeStore, leaseStore: new FakeCredentialLeaseStore());
        var acmeCredential = await acmeIssuer.IssueAsync(CreateRequest(lifetime: TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var acmeEastCredential = await acmeEastIssuer.IssueAsync(CreateRequest(lifetime: TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        // Both credentials are now expired, but tenant "acme" must only ever reclaim its own rows.
        clock.SetupGet(value => value.UtcNow).Returns(now.AddMinutes(2));

        // Act
        var removed = await acmeIssuer.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, removed);
        Assert.Contains(acmeCredential.AuthorizationUser, sharedRealtimeStore.Deleted);
        Assert.DoesNotContain(acmeEastCredential.AuthorizationUser, sharedRealtimeStore.Deleted);
        Assert.NotEqual(acmeCredential.AuthorizationUser, acmeEastCredential.AuthorizationUser);
    }

    [Fact]
    public async Task RevokeAsync_RejectsCredentialFromAnotherTenant()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var tenantAIssuer = CreateIssuer("TenantA", cache);
        var tenantBIssuer = CreateIssuer("TenantB", cache);
        var credential = await tenantAIssuer.IssueAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Act
        var revoked = await tenantBIssuer.RevokeAsync(credential.AuthorizationUser, "test", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(revoked);
    }

    [Fact]
    public async Task IssueAsync_ConcurrentIssuesForSameTenant_BothRemainDiscoverableInLeaseStore()
    {
        // Arrange
        var issuer = CreateIssuer("TenantA");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = Task.Run(() => issuer.IssueAsync(CreateRequest(), cancellationToken), cancellationToken);
        var second = Task.Run(() => issuer.IssueAsync(CreateRequest(), cancellationToken), cancellationToken);
        var credentials = await Task.WhenAll(first, second);

        // Assert
        var live = issuer.LiveLeases();
        Assert.Contains(live, lease => lease.AuthorizationUser == credentials[0].AuthorizationUser);
        Assert.Contains(live, lease => lease.AuthorizationUser == credentials[1].AuthorizationUser);
        Assert.Equal(2, live.Count);
    }

    [Fact]
    public async Task IssueAsync_WhenRealtimeProvisionFails_LeaseIsRevokedAndCleanupReclaims()
    {
        // Arrange
        var store = new ThrowingUpsertRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", store: store);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: the realtime write fails after the durable lease was persisted.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.IssueAsync(CreateRequest(), cancellationToken));

        // Assert: the lease survives and is marked revoked so cleanup can reclaim any partial row by exact id.
        var lease = Assert.Single(issuer.AllLeases());
        Assert.NotNull(lease.RevokedUtc);
        Assert.Equal("provision_failed", lease.RevocationReason);

        var removed = await issuer.CleanupExpiredAsync(cancellationToken);

        Assert.Equal(1, removed);
        Assert.Contains(lease.AuthorizationUser, store.Deleted);
        Assert.Empty(issuer.AllLeases());
    }

    [Fact]
    public async Task IssueAsync_WhenUserExceedsCredentialCap_RevokesOldestCredential()
    {
        // Arrange
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", store: store);
        var cancellationToken = TestContext.Current.CancellationToken;
        var credentials = new List<AsteriskPjsipCredential>();

        // Act
        for (var index = 0; index < 4; index++)
        {
            credentials.Add(await issuer.IssueAsync(CreateRequest(userId: "user-capped"), cancellationToken));
        }

        // Assert
        Assert.Contains(credentials[0].AuthorizationUser, store.Deleted);

        var live = issuer.LiveLeases();
        Assert.DoesNotContain(live, lease => lease.AuthorizationUser == credentials[0].AuthorizationUser);
        Assert.Equal(3, live.Count);
    }

    [Fact]
    public async Task IssueAsync_CapEnforcedFromDurableLease_SurvivesCacheEviction()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", cache, store: store);
        var cancellationToken = TestContext.Current.CancellationToken;
        var credentials = new List<AsteriskPjsipCredential>();

        for (var index = 0; index < 3; index++)
        {
            credentials.Add(await issuer.IssueAsync(CreateRequest(userId: "user-capped"), cancellationToken));
        }

        // Evict the entire cache; the cap must still be enforced from the durable lease store.
        cache.Clear();

        // Act
        credentials.Add(await issuer.IssueAsync(CreateRequest(userId: "user-capped"), cancellationToken));

        // Assert
        Assert.Contains(credentials[0].AuthorizationUser, store.Deleted);
        Assert.Equal(3, issuer.LiveLeases().Count);
    }

    [Fact]
    public async Task IssueAsync_WhenUserIdMissing_Throws()
    {
        // Arrange
        var issuer = CreateIssuer("TenantA");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            issuer.IssueAsync(CreateRequest(userId: null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IssueAsync_GeneratesServerOwnedSession_IgnoringCallerSuppliedInteractionId()
    {
        // Arrange
        var issuer = CreateIssuer("TenantA");

        // Act
        var credential = await issuer.IssueAsync(
            CreateRequest(sessionId: null, interactionId: "attacker-supplied"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(credential.SessionId));
        Assert.NotEqual("attacker-supplied", credential.SessionId);

        // The interaction id is carried only as non-authoritative metadata on the durable lease.
        var lease = Assert.Single(issuer.AllLeases());
        Assert.Equal("attacker-supplied", lease.InteractionId);
        Assert.Equal(credential.SessionId, lease.SessionId);
    }

    [Fact]
    public async Task RevokeUserAsync_RevokesEveryCredentialOwnedByUser_SurvivesCacheEviction()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var store = new TestRealtimeCredentialStore();
        var issuer = CreateIssuer("TenantA", cache, store: store);
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await issuer.IssueAsync(CreateRequest(userId: "user-signout"), cancellationToken);
        var second = await issuer.IssueAsync(CreateRequest(userId: "user-signout"), cancellationToken);

        // Evict the cache so revocation must locate live credentials via the durable lease store.
        cache.Clear();

        // Act
        var revoked = await issuer.RevokeUserAsync("user-signout", "signed_out", cancellationToken);

        // Assert
        Assert.Equal(2, revoked);
        Assert.Contains(first.AuthorizationUser, store.Deleted);
        Assert.Contains(second.AuthorizationUser, store.Deleted);
        Assert.Empty(issuer.AllLeases());
    }

    private static TestIssuer CreateIssuer(
        string tenantName,
        IDistributedCache cache = null,
        Mock<IClock> clock = null,
        TestRealtimeCredentialStore store = null,
        TestDialogTerminator terminator = null,
        FakeCredentialLeaseStore leaseStore = null)
        => CreateIssuer(tenantName, cache, clock, (IAsteriskPjsipRealtimeCredentialStore)store, terminator, leaseStore);

    private static TestIssuer CreateIssuer(
        string tenantName,
        IDistributedCache cache,
        Mock<IClock> clock,
        IAsteriskPjsipRealtimeCredentialStore store,
        TestDialogTerminator terminator = null,
        FakeCredentialLeaseStore leaseStore = null)
    {
        cache ??= new FakeDistributedCache();
        clock ??= new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        store ??= new TestRealtimeCredentialStore();
        terminator ??= new TestDialogTerminator();
        leaseStore ??= new FakeCredentialLeaseStore();

        var tagCache = new Mock<ITagCache>();
        tagCache.Setup(cache => cache.RemoveTagAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        return new TestIssuer(
            cache,
            leaseStore,
            new AsteriskPjsipCredentialIssuer(
                cache,
                tagCache.Object,
                new FakeDistributedLock(),
                clock.Object,
                new ShellSettings { Name = tenantName },
                store,
                leaseStore,
                terminator),
            tenantName);
    }

    private static AsteriskPjsipCredentialIssueRequest CreateRequest(
        string sessionId = "interaction-1",
        TimeSpan? lifetime = null,
        string userId = "user-1",
        string interactionId = null)
        => new()
        {
            UserId = userId,
            DisplayName = "Agent One",
            SessionId = sessionId,
            InteractionId = interactionId,
            SipDomain = "pbx.example.test",
            CredentialLifetime = lifetime ?? TimeSpan.FromMinutes(15),
            ContactExpiration = TimeSpan.FromSeconds(120),
            Codecs = ["opus", "g722", "ulaw"],
        };

    private sealed class TestIssuer
    {
        private readonly string _tenantName;
        private readonly FakeCredentialLeaseStore _leaseStore;

        public TestIssuer(
            IDistributedCache cache,
            FakeCredentialLeaseStore leaseStore,
            AsteriskPjsipCredentialIssuer issuer,
            string tenantName)
        {
            Cache = cache;
            _leaseStore = leaseStore;
            Issuer = issuer;
            _tenantName = tenantName;
        }

        public IDistributedCache Cache { get; }

        public AsteriskPjsipCredentialIssuer Issuer { get; }

        public Task<AsteriskPjsipCredential> IssueAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken)
            => Issuer.IssueAsync(request, cancellationToken);

        public Task<AsteriskPjsipCredential> RotateAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken)
            => Issuer.RotateAsync(request, cancellationToken);

        public Task<bool> RevokeAsync(
            string authorizationUser,
            string reason,
            CancellationToken cancellationToken)
            => Issuer.RevokeAsync(authorizationUser, reason, cancellationToken);

        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
            => Issuer.CleanupExpiredAsync(cancellationToken);

        public Task<int> RevokeUserAsync(
            string userId,
            string reason,
            CancellationToken cancellationToken)
            => Issuer.RevokeUserAsync(userId, reason, cancellationToken);

        public string RecordKey(string authorizationUser)
            => $"CrestApps:Asterisk:PjsipCredentials:{_tenantName}:user:{authorizationUser}";

        public IReadOnlyList<AsteriskPjsipCredentialLease> AllLeases()
            => _leaseStore.Snapshot();

        public List<AsteriskPjsipCredentialLease> LiveLeases()
            => _leaseStore.Snapshot()
                .Where(lease => lease.RevokedUtc is null)
                .ToList();
    }

    private class TestRealtimeCredentialStore : IAsteriskPjsipRealtimeCredentialStore
    {
        public List<AsteriskPjsipRealtimeCredential> Upserted { get; } = [];

        public List<string> Deleted { get; } = [];

        public virtual Task UpsertAsync(
            AsteriskPjsipRealtimeCredential credential,
            CancellationToken cancellationToken = default)
        {
            Upserted.Add(credential);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string authorizationUser,
            CancellationToken cancellationToken = default)
        {
            Deleted.Add(authorizationUser);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingUpsertRealtimeCredentialStore : TestRealtimeCredentialStore
    {
        public override Task UpsertAsync(
            AsteriskPjsipRealtimeCredential credential,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Realtime provisioning failed.");
    }

    private sealed class TestDialogTerminator : IAsteriskPjsipDialogTerminator
    {
        public List<(string AuthorizationUser, string Reason)> Terminated { get; } = [];

        public Task TerminateAsync(
            string authorizationUser,
            string reason,
            CancellationToken cancellationToken = default)
        {
            Terminated.Add((authorizationUser, reason));

            return Task.CompletedTask;
        }
    }
}

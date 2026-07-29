using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentSessionServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConnectAsync_WhenNoSession_CreatesOnlineSessionWithProfileMembership()
    {
        // Arrange
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentSession)null);
        sessionManager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentSession { ItemId = "s1" });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
                DisplayName = "Agent One",
                QueueIds = ["q1", "q2"],
                AllowedQueueIds = ["q1", "q2"],
            });

        var service = CreateService(sessionManager, agentManager);

        // Act
        var session = await service.ConnectAsync("u1", "c1", "user1", "User One", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.IsOnline);
        Assert.Contains("c1", session.ConnectionIds);
        Assert.Equal(["q1", "q2"], session.QueueIds);
        Assert.Equal(_now, session.LastHeartbeatUtc);
        sessionManager.Verify(m => m.CreateAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_WhenSessionExists_AddsConnectionAndUpdates()
    {
        // Arrange
        var existing = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1"], IsOnline = true };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);

        var service = CreateService(sessionManager, agentManager);

        // Act
        var session = await service.ConnectAsync("u1", "c2", "user1", "User One", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["c1", "c2"], session.ConnectionIds);
        Assert.True(session.IsOnline);
        sessionManager.Verify(m => m.UpdateAsync(It.IsAny<AgentSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        sessionManager.Verify(m => m.CreateAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_WhenLastConnection_MarksOffline()
    {
        // Arrange
        var existing = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1"], IsOnline = true };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService(sessionManager, new Mock<IAgentProfileManager>());

        // Act
        var session = await service.DisconnectAsync("u1", "c1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(session.IsOnline);
        Assert.Empty(session.ConnectionIds);
        Assert.Equal(_now, session.LastDisconnectedUtc);
    }

    [Fact]
    public async Task DisconnectAsync_WhenLastConnection_DoesNotSignOutAvailableProfileToday()
    {
        // Arrange
        var existing = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1"], IsOnline = true };
        var profile = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
        };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        var presenceManager = new Mock<IAgentPresenceManager>();
        var service = CreateService(sessionManager, agentManager, presenceManager);

        // Act
        var session = await service.DisconnectAsync("u1", "c1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(session.IsOnline);
        agentManager.Verify(
            m => m.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        presenceManager.Verify(
            m => m.SignOutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_WhenOtherConnectionsRemain_StaysOnline()
    {
        // Arrange
        var existing = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1", "c2"], IsOnline = true };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService(sessionManager, new Mock<IAgentProfileManager>());

        // Act
        var session = await service.DisconnectAsync("u1", "c1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.IsOnline);
        Assert.Equal(["c2"], session.ConnectionIds);
    }

    [Fact]
    public async Task HeartbeatAsync_UpdatesLastHeartbeat()
    {
        // Arrange
        var existing = new AgentSession { ItemId = "s1", UserId = "u1", LastHeartbeatUtc = _now.AddMinutes(-5) };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService(sessionManager, new Mock<IAgentProfileManager>());

        // Act
        var session = await service.HeartbeatAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, session.LastHeartbeatUtc);
        sessionManager.Verify(m => m.UpdateAsync(It.IsAny<AgentSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HeartbeatAsync_WhenNoSession_ReturnsNull()
    {
        // Arrange
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentSession)null);

        var service = CreateService(sessionManager, new Mock<IAgentProfileManager>());

        // Act
        var session = await service.HeartbeatAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task BuildSnapshotAsync_CombinesProfileAndSession()
    {
        // Arrange
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSession { ItemId = "s1", UserId = "u1", IsOnline = true, LastHeartbeatUtc = _now });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
                DisplayName = "Agent One",
                PresenceStatus = AgentPresenceStatus.Available,
                PresenceReason = "Ready",
                ActiveReservationId = "r1",
                QueueIds = ["q1"],
                AllowedQueueIds = ["q1"],
            });

        var service = CreateService(sessionManager, agentManager);

        // Act
        var snapshot = await service.BuildSnapshotAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.HasProfile);
        Assert.Equal("Available", snapshot.PresenceStatus);
        Assert.Equal("Ready", snapshot.PresenceReason);
        Assert.Equal("r1", snapshot.ActiveReservationId);
        Assert.Equal(["q1"], snapshot.QueueIds);
        Assert.True(snapshot.IsOnline);
        Assert.Equal(_now, snapshot.ServerTimeUtc);
    }

    [Fact]
    public async Task BuildSnapshotAsync_WhenLegacyMembershipIsNotEntitled_ExcludesIt()
    {
        // Arrange
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSession
            {
                ItemId = "s1",
                UserId = "u1",
                QueueIds = ["q1", "q2"],
            });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
                QueueIds = ["q1", "q2"],
                AllowedQueueIds = ["q1"],
            });

        var service = CreateService(sessionManager, agentManager);

        // Act
        var snapshot = await service.BuildSnapshotAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["q1"], snapshot.QueueIds);
    }

    [Fact]
    public async Task BuildSnapshotAsync_WhenProfileIsMissing_DoesNotTrustSessionMembership()
    {
        // Arrange
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSession
            {
                ItemId = "s1",
                UserId = "u1",
                QueueIds = ["q1"],
                CampaignIds = ["c1"],
            });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentProfile)null);

        var service = CreateService(sessionManager, agentManager);

        // Act
        var snapshot = await service.BuildSnapshotAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(snapshot.QueueIds);
        Assert.Empty(snapshot.CampaignIds);
    }

    [Fact]
    public async Task ExpireStaleAsync_SignsOutAndDeletesStaleSession()
    {
        // Arrange
        var stale = new AgentSession { ItemId = "s1", UserId = "u1", IsOnline = true, LastHeartbeatUtc = _now.AddMinutes(-5) };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.ListStaleAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([stale]);
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(stale);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available });

        var presenceManager = new Mock<IAgentPresenceManager>();

        var service = CreateService(sessionManager, agentManager, presenceManager);

        // Act
        var count = await service.ExpireStaleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        presenceManager.Verify(m => m.SignOutAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        sessionManager.Verify(m => m.DeleteAsync(stale, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireStaleAsync_WhenHeartbeatRefreshed_SkipsSession()
    {
        // Arrange
        var staleCandidate = new AgentSession { ItemId = "s1", UserId = "u1", IsOnline = true, LastHeartbeatUtc = _now.AddMinutes(-5) };
        var refreshed = new AgentSession { ItemId = "s1", UserId = "u1", IsOnline = true, LastHeartbeatUtc = _now };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.ListStaleAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([staleCandidate]);
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(refreshed);

        var presenceManager = new Mock<IAgentPresenceManager>();

        var service = CreateService(sessionManager, new Mock<IAgentProfileManager>(), presenceManager);

        // Act
        var count = await service.ExpireStaleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        presenceManager.Verify(m => m.SignOutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sessionManager.Verify(m => m.DeleteAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HeartbeatAsync_DoesNotThrowAtTheAgent_WhenTheStampLosesItsVersionCheck()
    {
        // Arrange
        // The heartbeat rewrites the whole session document, so it competes with connect, disconnect and the
        // cleanup pass for the document version. Losing that race must not surface to the agent: the heartbeat
        // arrives on a timer and carries nothing the agent needs, and whichever writer won has already moved
        // the session forward. The caller is told what is stored rather than what the stamp tried to write.
        var stored = new AgentSession { ItemId = "s1", UserId = "u1", LastHeartbeatUtc = _now.AddMinutes(-5) };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var service = CreateService(
            sessionManager,
            new Mock<IAgentProfileManager>(),
            scopeExecutor: new ThrowingScopeExecutor(new ConcurrencyException(new Document())));

        // Act
        var session = await service.HeartbeatAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(stored, session);
        Assert.Equal(_now.AddMinutes(-5), session.LastHeartbeatUtc);
    }

    [Fact]
    public async Task HeartbeatAsync_StampsTheSessionItReadsInItsOwnUnitOfWork_SoAConcurrentConnectIsNotUndone()
    {
        // Arrange
        // A session read on the ambient unit of work can already be behind a connect that committed since, and
        // re-reading it there returns the same instance because the session answers from its identity map.
        // Writing that copy back would stamp the heartbeat and drop the connection the connect added, leaving
        // routing believing a connected agent is not. The stamp therefore runs in its own unit of work, whose
        // read reflects what is committed.
        var ambient = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1"], IsOnline = true, LastHeartbeatUtc = _now.AddMinutes(-5) };
        var committed = new AgentSession { ItemId = "s1", UserId = "u1", ConnectionIds = ["c1", "c2"], IsOnline = true, LastHeartbeatUtc = _now.AddMinutes(-5) };

        var ambientManager = new Mock<IAgentSessionManager>();
        ambientManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(ambient);

        var scopedManager = new Mock<IAgentSessionManager>();
        scopedManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(committed);

        AgentSession written = null;
        scopedManager
            .Setup(m => m.UpdateAsync(It.IsAny<AgentSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSession, JsonNode, CancellationToken>((session, _, _) => written = session);

        var service = CreateService(
            ambientManager,
            new Mock<IAgentProfileManager>(),
            scopeExecutor: new StubScopeExecutor(scopedManager.Object));

        // Act
        var session = await service.HeartbeatAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(written);
        Assert.Equal(["c1", "c2"], written.ConnectionIds);
        Assert.Equal(_now, written.LastHeartbeatUtc);
        Assert.Same(committed, session);

        // The ambient copy is never the one written, so a stale connection list cannot reach the store.
        ambientManager.Verify(m => m.UpdateAsync(It.IsAny<AgentSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HeartbeatAsync_TakesNoDistributedLock_BecauseTheWriteCommitsOutsideAnyLockItCouldHold()
    {
        // Arrange
        // Writes are staged, not committed, so the version check runs when the unit of work commits — after any
        // lock taken inside this method has been released. A lock here would cost every connected agent two
        // round trips on a timer and still not make the write exclusive.
        var stored = new AgentSession { ItemId = "s1", UserId = "u1", LastHeartbeatUtc = _now.AddMinutes(-5) };
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var distributedLock = CreateDistributedLock();
        var service = CreateService(
            sessionManager,
            new Mock<IAgentProfileManager>(),
            distributedLock: distributedLock,
            scopeExecutor: new StubScopeExecutor(sessionManager.Object));

        // Act
        await service.HeartbeatAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        distributedLock.Verify(
            l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    private static AgentSessionService CreateService(
        Mock<IAgentSessionManager> sessionManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IAgentPresenceManager> presenceManager = null,
        Mock<IDistributedLock> distributedLock = null,
        IContactCenterScopeExecutor scopeExecutor = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new AgentSessionService(
            sessionManager.Object,
            agentManager.Object,
            (presenceManager ?? new Mock<IAgentPresenceManager>()).Object,
            (distributedLock ?? CreateDistributedLock()).Object,
            scopeExecutor ?? new StubScopeExecutor(sessionManager.Object),
            clock.Object);
    }

    private static Mock<IDistributedLock> CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock;
    }

    /// <summary>
    /// Runs the operation against a supplied context, standing in for the child unit of work the service uses
    /// so a test can give the stamp a different view of the session than the ambient read has.
    /// </summary>
    private sealed class StubScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly object _context;

        public StubScopeExecutor(object context)
        {
            _context = context;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => operation((TContext)_context);

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }

    /// <summary>
    /// Fails the child unit of work, standing in for the version check the stamp loses when another writer
    /// commits the same session first.
    /// </summary>
    private sealed class ThrowingScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly Exception _exception;

        public ThrowingScopeExecutor(Exception exception)
        {
            _exception = exception;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => throw _exception;

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }
}

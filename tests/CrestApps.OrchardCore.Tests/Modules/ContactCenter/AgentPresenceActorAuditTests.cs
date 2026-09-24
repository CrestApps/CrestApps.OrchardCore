using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The presence event a state change publishes names the same actor, and the same instant, as the state change it
/// accompanies. It used to put the agent's user id where the actor goes and leave the kind of actor unsaid, so a
/// wrap-up the platform started read as something nobody in particular did, and one dated by the provider's hangup
/// was stamped with the server's clock instead.
/// </summary>
public sealed class AgentPresenceActorAuditTests
{
    private static readonly DateTime _now = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SetPresenceAsync_ByTheAgent_NamesTheAgent()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);

        // Assert
        AssertPresenceMatchesState(fixture.Log, ContactCenterActorType.Agent, "u1");
    }

    [Fact]
    public async Task SetPresenceAsync_ByASupervisor_NamesTheSupervisor()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SetPresenceAsync(
            "u1",
            AgentPresenceStatus.Break,
            "Coaching",
            new AgentStateChangeContext { Actor = ContactCenterActor.Supervisor("sup-1") },
            TestContext.Current.CancellationToken);

        // Assert
        AssertPresenceMatchesState(fixture.Log, ContactCenterActorType.Supervisor, "sup-1");
    }

    [Fact]
    public async Task StartWrapUpAsync_NamesThePlatform_AndIsDatedByTheHangupThatCausedIt()
    {
        // Arrange
        // The provider's clock runs ahead of this one, so the hangup that starts wrap-up is dated after the moment
        // the platform hears of it. The wrap-up is the hangup's consequence and carries its instant.
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Busy));
        var providerHangupUtc = _now.AddMilliseconds(445);

        // Act
        await fixture.Service.StartWrapUpAsync(
            "a1",
            new AgentStateChangeContext { InteractionId = "int-1", ChangedUtc = providerHangupUtc },
            TestContext.Current.CancellationToken);

        // Assert
        var (state, presence) = AssertPresenceMatchesState(fixture.Log, ContactCenterActorType.System, ContactCenterConstants.SystemActor);
        Assert.Equal(providerHangupUtc, state.OccurredUtc);
        Assert.Equal(providerHangupUtc, presence.OccurredUtc);
        Assert.Equal(providerHangupUtc, presence.GetData<AgentPresenceChangedEventData>().ChangedUtc);

        // Recorded when the platform learned of it, which is the local clock.
        Assert.Equal(_now, presence.RecordedUtc);
    }

    [Fact]
    public async Task CompleteWorkAsync_NamesThePlatform()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.WrapUp);
        profile.RequestedPresenceStatus = AgentPresenceStatus.Available;
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.CompleteWorkAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        AssertPresenceMatchesState(fixture.Log, ContactCenterActorType.System, ContactCenterConstants.SystemActor);
    }

    [Fact]
    public async Task SignInAndSignOut_NameTheAgent_AndSayWhichAgentTheyAreAbout()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Offline));

        // Act
        await fixture.Service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);
        await fixture.Service.SignOutAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        foreach (var eventType in new[] { ContactCenterConstants.Events.AgentSignedIn, ContactCenterConstants.Events.AgentSignedOut })
        {
            var presence = fixture.Log.Single(eventType);
            Assert.Equal(ContactCenterActorType.Agent, presence.ActorType);
            Assert.Equal("u1", presence.ActorId);
            Assert.Equal("a1", presence.AggregateId);
            Assert.Equal("a1", presence.GetData<AgentPresenceChangedEventData>().AgentId);
            Assert.Equal("u1", presence.GetData<AgentPresenceChangedEventData>().UserId);
        }

        fixture.Log.AssertEveryEventNamesItsActor();
    }

    [Fact]
    public async Task MarkOfflineAsync_NamesThePlatform()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.MarkOfflineAsync("u1", "session-expired", TestContext.Current.CancellationToken);

        // Assert
        AssertPresenceMatchesState(fixture.Log, ContactCenterActorType.System, ContactCenterConstants.SystemActor);
    }

    private static (InteractionEvent State, InteractionEvent Presence) AssertPresenceMatchesState(
        AuditedEventLog log,
        ContactCenterActorType actorType,
        string actorId)
    {
        var state = log.Single(ContactCenterConstants.Events.AgentStateChanged);
        var presence = log.Single(ContactCenterConstants.Events.AgentPresenceChanged);

        Assert.Equal(actorType, state.ActorType);
        Assert.Equal(actorId, state.ActorId);
        Assert.Equal(state.ActorType, presence.ActorType);
        Assert.Equal(state.ActorId, presence.ActorId);
        Assert.Equal(state.OccurredUtc, presence.OccurredUtc);

        // The agent is the subject, carried where the timeline and the workflows find it.
        Assert.Equal("a1", presence.AggregateId);
        Assert.Equal("u1", presence.GetData<AgentPresenceChangedEventData>().UserId);

        log.AssertEveryEventNamesItsActor();

        return (state, presence);
    }

    private static AgentProfile CreateProfile(AgentPresenceStatus status)
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = status,
            PresenceChangedUtc = _now.AddMinutes(-30),
            AllowedQueueIds = ["q1", "q2"],
            QueueIds = ["q1"],
        };

    private sealed class Fixture
    {
        public Fixture(AgentProfile profile)
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));

            Log = new AuditedEventLog(Clock);

            Service = new AgentPresenceManagerService(
                agentManager.Object,
                [new Mock<IAgentSessionManager>().Object],
                new NoAgentWorkStateHealingService(),
                new EnforcingAgentEntitlementPolicy(),
                AgentStateAuditTestDoubles.CreateTransitions(Log.Recorder, Clock),
                Log.PublisherMock.Object,
                distributedLock.Object,
                Clock,
                NullLogger<AgentPresenceManagerService>.Instance);
        }

        public AdvanceableClock Clock { get; } = new(_now);

        public AuditedEventLog Log { get; }

        public AgentPresenceManagerService Service { get; }
    }
}

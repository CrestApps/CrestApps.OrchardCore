using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A supervisor taking a queue call over, end to end on the real SQLite store: the agent answered the call, the
/// supervisor barged in on their own phone and took it over, and later the customer hung up. The call, its talk time
/// and its after-call work move to the supervisor at the takeover; the agent's part of the call ends there.
/// </summary>
public sealed class SupervisorTakeoverIntegrationTests
{
    private const string SupervisorLeg = "supervisor-leg";

    [Fact]
    public async Task TakingACallOver_MovesItsOwnershipTalkTimeAndWrapUpToTheSupervisor()
    {
        // Arrange
        var provider = new FakeSupervisionVoiceProvider();
        await using var fixture = await TransferIntegrationFixture.CreateAsync(provider);
        var harness = fixture.Harness;
        var services = harness.Services;
        var interaction = await fixture.FindInteractionAsync();
        var answeredUtc = harness.Clock.UtcNow;
        var supervisor = TransferIntegrationFixture.Principal(TransferIntegrationFixture.UserB);
        var parts = Compose(services, provider);

        // Act: the supervisor barges in, their phone answers, a minute later they take the call over.
        var engaged = await parts.Monitoring.EngageAsync(interaction.ItemId, TransferIntegrationFixture.UserB, supervisor, MonitorMode.Barge, TestContext.Current.CancellationToken);
        await harness.CommitAsync();
        Assert.True(engaged.Succeeded, engaged.Reason);

        Assert.True(await parts.Sink.OnAnsweredAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, SupervisorLeg, TestContext.Current.CancellationToken));
        await harness.CommitAsync();

        harness.Clock.Advance(TimeSpan.FromSeconds(60));
        var takeoverUtc = harness.Clock.UtcNow;

        var takenOver = await parts.Interventions.TakeOverAsync(interaction.ItemId, TransferIntegrationFixture.UserB, supervisor, TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        // The provider released the agent's leg; its end is reported back and must not end the call.
        var agentLegEndedTheCall = await services.GetRequiredService<IContactCenterAgentLegFailureService>().RecordEndedAsync(
            DialerModeIntegrationHarness.ProviderName,
            fixture.CallId,
            TransferIntegrationFixture.AgentLegA,
            takeoverUtc,
            HangupCause.NormalClearing,
            TestContext.Current.CancellationToken);
        await harness.CommitAsync();

        // Assert: the call is the supervisor's.
        Assert.True(takenOver.Succeeded, takenOver.Reason);
        Assert.False(agentLegEndedTheCall);
        Assert.Equal([TransferIntegrationFixture.AgentLegA], provider.ReleasedByTakeover);

        interaction = await fixture.FindInteractionAsync();
        var session = await fixture.FindSessionAsync();
        Assert.False(interaction.IsSettled);
        Assert.Equal(TransferIntegrationFixture.AgentB, interaction.AgentId);
        Assert.Equal(TransferIntegrationFixture.AgentB, session.AgentId);
        Assert.NotNull(session.Legs.Single(leg => leg.ProviderLegId == TransferIntegrationFixture.AgentLegA).EndedUtc);
        Assert.Equal(TransferIntegrationFixture.AgentB, session.Legs.Single(leg => leg.ProviderLegId == SupervisorLeg && leg.Role == CallPartyRole.Agent).AgentId);
        Assert.Empty(session.ActiveMonitorSessions);

        // The agent's work ended the way a queue call's does; the supervisor is on a call.
        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(TransferIntegrationFixture.AgentA));
        Assert.Equal(AgentPresenceStatus.Busy, await harness.GetPresenceAsync(TransferIntegrationFixture.AgentB));

        // Act: ninety seconds later the customer hangs up.
        harness.Clock.Advance(TimeSpan.FromSeconds(90));
        var endedUtc = harness.Clock.UtcNow;
        await fixture.CallerHangsUpAsync();

        // Assert: the supervisor's after-call work, and the talk time split at the takeover.
        Assert.Equal(AgentPresenceStatus.WrapUp, await harness.GetPresenceAsync(TransferIntegrationFixture.AgentB));
        Assert.True((await fixture.FindInteractionAsync()).IsSettled);

        var takeover = Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.SupervisorTookOver);
        Assert.Equal(TransferIntegrationFixture.UserB, takeover.ActorId);
        Assert.Equal(TransferIntegrationFixture.AgentA, takeover.GetData<CallLifecycleEventData>().AgentId);

        var callEvents = fixture.Events.Where(e => CallHandlingMetrics.CallEventTypes.Contains(e.EventType)).ToArray();
        var metrics = CallHandlingMetrics.Calculate(callEvents, [], endedUtc.AddMinutes(1));
        var agent = metrics.Agents.Single(value => value.AgentId == TransferIntegrationFixture.AgentA);
        var taker = metrics.Agents.Single(value => value.AgentId == TransferIntegrationFixture.AgentB);
        var agentConnectedUtc = callEvents
            .Where(e => e.EventType is ContactCenterConstants.Events.AgentLegAnswered or ContactCenterConstants.Events.CallConnected &&
                e.GetData<CallLifecycleEventData>()?.AgentId == TransferIntegrationFixture.AgentA)
            .Min(e => e.OccurredUtc);

        Assert.True(agentConnectedUtc <= answeredUtc);
        Assert.Equal((takeoverUtc - agentConnectedUtc).TotalSeconds, agent.ConnectedSeconds, precision: 0);
        Assert.Equal((endedUtc - takeoverUtc).TotalSeconds, taker.ConnectedSeconds, precision: 0);
        Assert.Equal(1, taker.CallsHandled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnAgentsTransfer_ReleasesTheSupervisorsOnTheCallFirst(bool supervised)
    {
        // Arrange - a supervisor is listening to the agent's call, or nobody is.
        await using var fixture = await TransferIntegrationFixture.CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        if (supervised)
        {
            var session = await fixture.FindSessionAsync();
            CallTopologyProjector.StartMonitorSession(session, "monitor-1", TransferIntegrationFixture.UserB, TransferIntegrationFixture.AgentB, MonitorMode.Monitor, fixture.Harness.Clock.UtcNow, SupervisorLeg);
            await fixture.Sessions.UpdateAsync(session, cancellationToken: TestContext.Current.CancellationToken);
            await fixture.Harness.CommitAsync();
        }

        // Act - the agent sends the call to another queue.
        var result = await fixture.TransferService.TransferAsync(
            TransferIntegrationFixture.BlindRequest(InteractionTransferTargetType.Queue, TransferIntegrationFixture.SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);

        // Assert - nobody listening can follow it, so they were let go before it moved.
        Assert.True(result.Succeeded, result.Reason);
        fixture.Monitoring.Verify(
            value => value.ForceDisengageAllAsync(interaction.ItemId, "transfer", It.IsAny<CancellationToken>()),
            supervised ? Times.Once() : Times.Never());
    }

    private static (ContactCenterMonitoringService Monitoring, SupervisorLegEventSink Sink, ContactCenterSupervisorInterventionService Interventions) Compose(
        IServiceProvider services,
        FakeSupervisionVoiceProvider provider)
    {
        var clock = services.GetRequiredService<IClock>();
        var interactions = services.GetRequiredService<IInteractionManager>();
        var sessions = services.GetRequiredService<ICallSessionManager>();
        var agents = services.GetRequiredService<IAgentProfileManager>();
        var publisher = services.GetRequiredService<IContactCenterEventPublisher>();
        var executor = new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>());

        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(value => value.Get(It.IsAny<string>())).Returns(provider);

        // The supervisor supervises the call's queue.
        var supervisedQueues = new Mock<ISupervisorQueueAuthorizationService>();
        supervisedQueues
            .Setup(value => value.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), TransferIntegrationFixture.UserB, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var callControl = new CallControlAuthorizationService(agents, sessions, interactions, supervisedQueues.Object);
        var monitoring = new ContactCenterMonitoringService(interactions, sessions, resolver.Object, publisher, executor, callControl, clock, [], agents, new InPlaceCallSessionUpdater(interactions, sessions));
        var sink = new SupervisorLegEventSink(interactions, sessions, services.GetRequiredService<IContactCenterAgentLegFailureService>(), publisher, [], clock);

        var interventions = new ContactCenterSupervisorInterventionService(
            interactions,
            sessions,
            resolver.Object,
            callControl,
            new GrantingAuthorizationService(),
            supervisedQueues.Object,
            agents,
            services.GetRequiredService<IAgentPresenceManager>(),
            monitoring,
            Mock.Of<IContactCenterTransferService>(),
            [],
            services.GetRequiredService<IProviderVoiceEventService>(),
            Mock.Of<ITelephonyService>(),
            services.GetRequiredService<IContactCenterAuditRecorder>(),
            publisher,
            executor,
            [],
            clock,
            new InPlaceCallSessionUpdater(interactions, sessions),
            NullLogger<ContactCenterSupervisorInterventionService>.Instance);

        return (monitoring, sink, interventions);
    }

    private sealed class GrantingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    /// <summary>
    /// The provider seam for supervision: it rings the supervisor, changes their role, hands the call over and releases
    /// legs, recording each.
    /// </summary>
    private sealed class FakeSupervisionVoiceProvider :
        IContactCenterVoiceProvider,
        IContactCenterVoiceMonitoringProvider,
        IContactCenterVoiceSupervisorInterventionProvider,
        IContactCenterVoiceAgentLegReleaseProvider
    {
        public string TechnicalName => DialerModeIntegrationHarness.ProviderName;

        public Microsoft.Extensions.Localization.LocalizedString Name => new(TechnicalName, TechnicalName);

        public ContactCenterVoiceProviderCapabilities Capabilities
            => ContactCenterVoiceProviderCapabilities.AgentConnect |
                ContactCenterVoiceProviderCapabilities.Monitor |
                ContactCenterVoiceProviderCapabilities.Whisper |
                ContactCenterVoiceProviderCapabilities.Barge;

        public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

        public List<string> ReleasedByTakeover { get; } = [];

        public List<string> ReleasedLegs { get; } = [];

        public Task<ContactCenterVoiceProviderResult> EngageAsync(ContactCenterVoiceMonitoringRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName, ProviderLegId = SupervisorLeg });

        public Task<ContactCenterVoiceProviderResult> StopAsync(ContactCenterVoiceMonitoringRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName });

        public Task<ContactCenterVoiceProviderResult> SwitchModeAsync(ContactCenterVoiceMonitoringRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName });

        public Task<ContactCenterVoiceProviderResult> TakeOverAsync(ContactCenterVoiceMonitoringRequest request, CancellationToken cancellationToken = default)
        {
            ReleasedByTakeover.Add(request.AgentLegId);

            return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = TechnicalName });
        }

        public Task ReleaseSupervisorLegAsync(string supervisorLegId, CancellationToken cancellationToken = default)
        {
            ReleasedLegs.Add(supervisorLegId);

            return Task.CompletedTask;
        }

        public Task ReleaseAgentLegAsync(string agentLegId, CancellationToken cancellationToken = default)
        {
            ReleasedLegs.Add(agentLegId);

            return Task.CompletedTask;
        }
    }
}

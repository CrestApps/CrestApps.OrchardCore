using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security;
using ContactCenterTransferRequest = CrestApps.OrchardCore.ContactCenter.Core.Models.TransferRequest;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The supervisor interventions beyond listening: each one authorized against the intervention permission and the
/// supervisor's queue scope, each one changing exactly what it names, and each one recorded under the supervisor.
/// </summary>
public sealed class SupervisorInterventionServiceTests
{
    private static readonly ClaimsPrincipal _supervisor = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "sup1")], "Test"));

    [Fact]
    public async Task TakeOver_ReleasesTheAgent_AndMakesTheSupervisorTheAgentHandlingTheCall()
    {
        // Arrange
        var context = new Context(engagedMode: MonitorMode.Barge, connected: true);
        ContactCenterVoiceMonitoringRequest takenOver = null;
        context.Intervention
            .Setup(value => value.TakeOverAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => takenOver = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        // Act
        var result = await context.Service.TakeOverAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);

        Assert.Equal("call-1", takenOver.ProviderCallId);
        Assert.Equal("agent-leg", takenOver.AgentLegId);
        Assert.Equal("sup-leg", takenOver.SupervisorLegId);

        // The agent's leg ended; the supervisor's leg is now the call's agent leg, and the call is theirs.
        var session = context.Session;
        Assert.NotNull(session.Legs.Single(leg => leg.ProviderLegId == "agent-leg").EndedUtc);
        var supervisorLeg = session.Legs.Single(leg => leg.ProviderLegId == "sup-leg");
        Assert.Equal(CallPartyRole.Agent, supervisorLeg.Role);
        Assert.Equal("sup-agent", supervisorLeg.AgentId);
        Assert.NotNull(supervisorLeg.AnsweredUtc);
        Assert.Equal("sup-agent", session.AgentId);
        Assert.Equal("sup-agent", context.Interaction.AgentId);
        Assert.Empty(session.ActiveMonitorSessions);

        // Queue-routed work: the released agent goes to after-call work; the supervisor becomes busy.
        context.Presence.Verify(value => value.StartWrapUpAsync("agent-1", It.Is<AgentStateChangeContext>(change => change.Actor == ContactCenterActor.Supervisor("sup1")), It.IsAny<CancellationToken>()), Times.Once);
        context.Presence.Verify(value => value.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Presence.Verify(value => value.StartConsultWorkAsync("sup-agent", It.Is<AgentStateChangeContext>(change => change.Source == AgentStateChangeSources.SupervisorTakeover), It.IsAny<CancellationToken>()), Times.Once);

        context.Audit.Verify(value => value.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorTookOver,
            It.Is<CallLifecycleEventData>(data => data.AgentId == "agent-1" && data.ProviderLegId == "agent-leg" && data.Target == "sup-agent"),
            It.IsAny<DateTime>(),
            ContactCenterActor.Supervisor("sup1"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Audit.Verify(value => value.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegAnswered,
            It.Is<CallLifecycleEventData>(data => data.AgentId == "sup-agent" && data.ProviderLegId == "sup-leg"),
            It.IsAny<DateTime>(),
            ContactCenterActor.Supervisor("sup1"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Contains(context.Notifier.Engagements, notification => notification.State == SupervisorEngagementNotification.TookOver);
    }

    [Fact]
    public async Task TakeOver_OfADirectCall_ReturnsTheAgentToReady()
    {
        // Arrange
        var context = new Context(engagedMode: MonitorMode.Monitor, connected: true, queueId: ContactCenterConstants.DirectRouting.QueueId);
        context.Intervention
            .Setup(value => value.TakeOverAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        // Act
        var result = await context.Service.TakeOverAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        context.Presence.Verify(value => value.CompleteWorkAsync("agent-1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Presence.Verify(value => value.StartWrapUpAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not-engaged")]
    [InlineData("not-connected")]
    [InlineData("secure-capture")]
    [InlineData("no-permission")]
    [InlineData("provider-refused")]
    public async Task TakeOver_IsRefused_AndChangesNothing(string why)
    {
        // Arrange
        var context = new Context(
            engagedMode: why == "not-engaged" ? null : MonitorMode.Barge,
            connected: why != "not-connected",
            recordingState: why == "secure-capture" ? RecordingState.Paused : RecordingState.Recording,
            grantIntervene: why != "no-permission");
        context.Intervention
            .Setup(value => value.TakeOverAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = why != "provider-refused" });

        // Act
        var result = await context.Service.TakeOverAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent-1", context.Session.AgentId);
        Assert.Equal("agent-1", context.Interaction.AgentId);
        Assert.Null(context.Session.Legs.Single(leg => leg.ProviderLegId == "agent-leg").EndedUtc);
        context.Presence.VerifyNoOtherCalls();

        if (why != "provider-refused")
        {
            context.Intervention.Verify(value => value.TakeOverAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task EndCall_HangsUpTheCustomer_AndEndsTheCallAsTheSupervisors()
    {
        // Arrange
        var context = new Context();
        context.Telephony
            .Setup(value => value.HangupAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(null));
        ProviderVoiceEvent ended = null;
        context.Ingestion
            .Setup(value => value.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((providerEvent, _) => ended = providerEvent)
            .ReturnsAsync(context.Session);

        // Act
        var result = await context.Service.EndCallAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(VoiceCallState.Ended, ended.State);
        Assert.Equal("call-1", ended.ProviderCallId);
        Assert.Equal("supervisor", ended.Metadata[ContactCenterConstants.TelephonyMetadata.HangupSource]);
        context.Audit.Verify(value => value.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorEndedCall,
            It.IsAny<CallLifecycleEventData>(),
            It.IsAny<DateTime>(),
            ContactCenterActor.Supervisor("sup1"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndCall_WhenTheHangupFails_DoesNotEndTheInteraction()
    {
        // Arrange
        var context = new Context();
        context.Telephony
            .Setup(value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Failed("unreachable"));

        // Act
        var result = await context.Service.EndCallAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        context.Ingestion.Verify(value => value.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EndCall_WithoutTheInterventionPermission_IsRefused()
    {
        // Arrange
        var context = new Context(grantIntervene: false);

        // Act
        var result = await context.Service.EndCallAsync("int1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        context.Telephony.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Transfer_ReleasesEverySupervisor_ThenTransfersAsASupervisorOperation()
    {
        // Arrange
        var context = new Context(engagedMode: MonitorMode.Monitor, connected: true);
        var order = new List<string>();
        context.Monitoring
            .Setup(value => value.ForceDisengageAllAsync("int1", "transfer", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("disengage"))
            .ReturnsAsync(1);
        ContactCenterTransferRequest transferred = null;
        context.Transfers
            .Setup(value => value.TransferAsync(It.IsAny<ContactCenterTransferRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterTransferRequest, CancellationToken>((request, _) =>
            {
                order.Add("transfer");
                transferred = request;
            })
            .ReturnsAsync(TransferResult.Success());

        // Act
        var result = await context.Service.TransferAsync("int1", "sup1", _supervisor, InteractionTransferTargetType.Queue, "queue-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(["disengage", "transfer"], order);
        Assert.True(transferred.SupervisorOperation);
        Assert.Equal(InteractionTransferType.Blind, transferred.Type);
        Assert.Equal(InteractionTransferTargetType.Queue, transferred.TargetType);
        Assert.Equal("queue-2", transferred.TargetId);
        Assert.Equal("sup1", transferred.InitiatedByUserId);
        Assert.Equal("agent-1", transferred.InitiatedByAgentId);
        context.Audit.Verify(value => value.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorTransferredCall,
            It.Is<CallLifecycleEventData>(data => data.Target == "Queue:queue-2"),
            It.IsAny<DateTime>(),
            ContactCenterActor.Supervisor("sup1"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RecordingState.None, true, "start")]
    [InlineData(RecordingState.Stopped, true, "start")]
    [InlineData(RecordingState.Recording, false, "stop")]
    public async Task SetRecording_StartsOrStopsTheRecording(RecordingState state, bool record, string expected)
    {
        // Arrange
        var context = new Context(recordingState: state);
        context.Recording.Setup(value => value.StartAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(RecordingCommandResult.Success());
        context.Recording.Setup(value => value.StopAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(RecordingCommandResult.Success());

        // Act
        var result = await context.Service.SetRecordingAsync("int1", "sup1", _supervisor, record, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        context.Recording.Verify(value => value.StartAsync("int1", It.IsAny<CancellationToken>()), expected == "start" ? Times.Once() : Times.Never());
        context.Recording.Verify(value => value.StopAsync("int1", It.IsAny<CancellationToken>()), expected == "stop" ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task SetRecording_DuringASensitiveDataCapture_IsRefused()
    {
        // Arrange
        var context = new Context(recordingState: RecordingState.Paused);

        // Act
        var result = await context.Service.SetRecordingAsync("int1", "sup1", _supervisor, record: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        context.Recording.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Available)]
    [InlineData(AgentPresenceStatus.Away)]
    [InlineData(AgentPresenceStatus.Break)]
    public async Task SetAgentState_SetsItUnderThePresenceRules_AsTheSupervisor(AgentPresenceStatus status)
    {
        // Arrange
        var context = new Context();
        context.Presence
            .Setup(value => value.SetPresenceAsync("agent-user", status, "Coaching", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user", PresenceStatus = status });

        // Act
        var result = await context.Service.SetAgentStateAsync("agent-1", "sup1", _supervisor, status, "Coaching", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        context.Presence.Verify(value => value.SetPresenceAsync(
            "agent-user",
            status,
            "Coaching",
            It.Is<AgentStateChangeContext>(change => change.Actor == ContactCenterActor.Supervisor("sup1")),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Publisher.Verify(value => value.PublishAsync(
            It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.SupervisorSetAgentState && e.AggregateId == "agent-1" && e.ActorId == "sup1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAgentState_OnABusyAgent_IsDeferredUntilTheirWorkEnds()
    {
        // Arrange
        var context = new Context();
        context.Presence
            .Setup(value => value.SetPresenceAsync("agent-user", AgentPresenceStatus.Break, null, It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user", PresenceStatus = AgentPresenceStatus.Busy, RequestedPresenceStatus = AgentPresenceStatus.Break });

        // Act
        var result = await context.Service.SetAgentStateAsync("agent-1", "sup1", _supervisor, AgentPresenceStatus.Break, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.WrapUp)]
    [InlineData(AgentPresenceStatus.Reserved)]
    [InlineData(AgentPresenceStatus.Offline)]
    public async Task SetAgentState_ToAStateThatFollowsWork_IsRefused(AgentPresenceStatus status)
    {
        // Arrange
        var context = new Context();

        // Act
        var result = await context.Service.SetAgentStateAsync("agent-1", "sup1", _supervisor, status, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        context.Presence.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetAgentState_ForAnAgentOutsideTheSupervisorsQueues_IsRefused()
    {
        // Arrange
        var context = new Context(supervisesQueue: false);

        // Act
        var result = await context.Service.SetAgentStateAsync("agent-1", "sup1", _supervisor, AgentPresenceStatus.Break, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        context.Presence.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignOutAgent_SignsThemOutOfTheirQueues_AsTheSupervisor()
    {
        // Arrange
        var context = new Context();

        // Act
        var result = await context.Service.SignOutAgentAsync("agent-1", "sup1", _supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        context.Presence.Verify(value => value.SignOutAsync("agent-user", It.Is<AgentStateChangeContext>(change => change.Actor == ContactCenterActor.Supervisor("sup1")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_DeliversItToTheAgent_AndRecordsIt()
    {
        // Arrange
        var context = new Context(grantIntervene: false);

        // Act
        var result = await context.Service.SendMessageAsync("agent-1", "sup1", "Sam Supervisor", _supervisor, "  Offer the retention discount.  ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        var message = Assert.Single(context.Notifier.Messages);
        Assert.Equal("agent-user", message.UserId);
        Assert.Equal("Offer the retention discount.", message.Text);
        Assert.Equal("Sam Supervisor", message.FromName);
        context.Publisher.Verify(value => value.PublishAsync(
            It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.SupervisorMessagedAgent && e.AggregateId == "agent-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_WithNothingToSay_IsRefused(string text)
    {
        // Arrange
        var context = new Context();

        // Act
        var result = await context.Service.SendMessageAsync("agent-1", "sup1", "Sam", _supervisor, text, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Notifier.Messages);
    }

    [Fact]
    public async Task SendMessage_TooLong_IsRefused()
    {
        // Arrange
        var context = new Context();

        // Act
        var result = await context.Service.SendMessageAsync("agent-1", "sup1", "Sam", _supervisor, new string('x', ContactCenterSupervisorInterventionService.MaximumMessageLength + 1), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Notifier.Messages);
    }

    private sealed class Context
    {
        public Context(
            MonitorMode? engagedMode = null,
            bool connected = false,
            RecordingState recordingState = RecordingState.Recording,
            bool grantIntervene = true,
            bool supervisesQueue = true,
            string queueId = "queue-1")
        {
            Session = SupervisorEngagementTests.Session(engagedMode);
            Session.QueueId = queueId;

            if (connected && Session.MonitorSessions.Count > 0)
            {
                Session.MonitorSessions[0].ConnectedUtc = new DateTime(2026, 9, 25, 10, 1, 1, DateTimeKind.Utc);
            }

            Interaction = new Interaction
            {
                ItemId = "int1",
                ProviderName = "p1",
                ProviderInteractionId = "call-1",
                AgentId = "agent-1",
                QueueId = queueId,
                RecordingState = recordingState,
            };

            var interactions = new Mock<IInteractionManager>();
            interactions.Setup(value => value.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(() => Interaction);

            var sessions = new Mock<ICallSessionManager>();
            sessions.Setup(value => value.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(() => Session);

            var provider = SupervisorEngagementTests.MonitoringProvider(out _, out var intervention);
            Intervention = intervention;

            var resolver = new Mock<IContactCenterVoiceProviderResolver>();
            resolver.Setup(value => value.Get("p1")).Returns(provider.Object);

            var agents = new Mock<IAgentProfileManager>();
            agents
                .Setup(value => value.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user", QueueIds = ["queue-1"], AllowedQueueIds = ["queue-1"] });

            var queues = new Mock<ISupervisorQueueAuthorizationService>();
            queues
                .Setup(value => value.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), "sup1", "queue-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(supervisesQueue);

            Service = new ContactCenterSupervisorInterventionService(
                interactions.Object,
                sessions.Object,
                resolver.Object,
                new FakeCallControlAuthorizationService(context => new CallControlAuthorizationResult
                {
                    Succeeded = true,
                    AgentId = "sup-agent",
                    ProviderCallId = "call-1",
                    CallSession = Session,
                }),
                new PermissionAuthorizationService(grantIntervene),
                queues.Object,
                agents.Object,
                Presence.Object,
                Monitoring.Object,
                Transfers.Object,
                [Recording.Object],
                Ingestion.Object,
                Telephony.Object,
                Audit.Object,
                Publisher.Object,
                new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
                [Notifier],
                new StubClock(),
                NullLogger<ContactCenterSupervisorInterventionService>.Instance);
        }

        public ContactCenterSupervisorInterventionService Service { get; }

        public CallSession Session { get; }

        public Interaction Interaction { get; }

        public Mock<IContactCenterVoiceSupervisorInterventionProvider> Intervention { get; }

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public Mock<IContactCenterMonitoringService> Monitoring { get; } = new();

        public Mock<IContactCenterTransferService> Transfers { get; } = new();

        public Mock<IContactCenterRecordingService> Recording { get; } = new();

        public Mock<IProviderVoiceEventService> Ingestion { get; } = new();

        public Mock<ITelephonyService> Telephony { get; } = new();

        public Mock<IContactCenterAuditRecorder> Audit { get; } = new();

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new();

        public SupervisorEngagementTests.RecordingNotifier Notifier { get; } = new();
    }

    // Grants MonitorContactCenter always, and the intervention permission when told to.
    private sealed class PermissionAuthorizationService : IAuthorizationService
    {
        private readonly bool _grantIntervene;

        public PermissionAuthorizationService(bool grantIntervene)
        {
            _grantIntervene = grantIntervene;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var granted = requirements.OfType<PermissionRequirement>().All(requirement =>
                requirement.Permission.Name == ContactCenterPermissions.MonitorContactCenter.Name ||
                (_grantIntervene && requirement.Permission.Name == ContactCenterPermissions.InterveneInCalls.Name));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}

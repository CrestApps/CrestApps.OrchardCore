using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A Power, Progressive or Predictive dial reserves the agent before the customer is dialed. Every state of that call
/// used to reach the agent's soft phone, so a number that was not in service showed "The number you dialed is not in
/// service." to an agent who never joined the call, although the dialer dispositions those attempts itself. The call
/// is the dialer's until the agent's leg answers, and only then reaches the soft phone.
/// </summary>
public sealed class ContactCenterSoftPhonePacedDialTests
{
    private static readonly ShellSettings _shellSettings = new() { Name = "TenantA" };

    [Fact]
    public async Task APowerDialToADeadNumber_NeverReachesTheReservedAgentsSoftPhone()
    {
        // Arrange
        var interaction = CreateDialerInteraction(InteractionStatus.Failed);
        var session = CreateSession(VoiceCallState.Failed, HangupCause.NotInService);
        var harness = new Harness(interaction, session, ActivitySources.PowerDial);

        // Act
        await harness.Handler.HandleAsync(CreateEvent(ContactCenterConstants.Events.CallEnded), TestContext.Current.CancellationToken);

        // Assert
        harness.Client.Verify(client => client.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Store.Verify(store => store.UpdateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ActivitySources.PowerDial)]
    [InlineData(ActivitySources.ProgressiveDial)]
    [InlineData(ActivitySources.PredictiveDial)]
    public async Task APacedDialStillBeingDialed_IsNotShownToTheReservedAgent(string source)
    {
        // Arrange
        var interaction = CreateDialerInteraction(InteractionStatus.Ringing);
        var session = CreateSession(VoiceCallState.Dialing, hangupCause: null);
        var harness = new Harness(interaction, session, source);

        // Act
        await harness.Handler.HandleAsync(CreateEvent(ContactCenterConstants.Events.CallSessionCreated), TestContext.Current.CancellationToken);

        // Assert
        harness.Client.Verify(client => client.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    [Fact]
    public async Task APreviewDialToADeadNumber_StillTellsTheAgentWhoPlacedIt()
    {
        // Arrange
        var interaction = CreateDialerInteraction(InteractionStatus.Failed);
        var session = CreateSession(VoiceCallState.Failed, HangupCause.NotInService);
        var harness = new Harness(interaction, session, ActivitySources.PreviewDial);

        // Act
        await harness.Handler.HandleAsync(CreateEvent(ContactCenterConstants.Events.CallEnded), TestContext.Current.CancellationToken);

        // Assert
        harness.Client.Verify(
            client => client.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                Equals(call.Metadata["hangupCause"], nameof(HangupCause.NotInService)))),
            Times.Once);
    }

    [Fact]
    public async Task APowerDialTheAgentJoined_ReachesTheSoftPhoneWhenTheAgentsLegAnswers()
    {
        // Arrange
        var interaction = CreateDialerInteraction(InteractionStatus.Connected);
        DialerCallMetadata.MarkAgentJoined(interaction, new DateTime(2026, 10, 7, 18, 0, 5, DateTimeKind.Utc));
        var session = CreateSession(VoiceCallState.Connected, hangupCause: null);
        var harness = new Harness(interaction, session, ActivitySources.PowerDial);

        // Act
        await harness.Handler.HandleAsync(CreateEvent(ContactCenterConstants.Events.AgentLegAnswered), TestContext.Current.CancellationToken);

        // Assert
        harness.Client.Verify(
            client => client.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Connected &&
                call.To == "+15550001000")),
            Times.Once);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnInboundAgentLegAnswer_IsNotProjected_SinceTheCallsOwnEventsAlreadyCarryIt()
    {
        // Arrange
        var interaction = CreateDialerInteraction(InteractionStatus.Connected);
        interaction.Direction = InteractionDirection.Inbound;
        interaction.TechnicalMetadata.Remove(DialerCallMetadata.DialerProfileIdKey);
        var session = CreateSession(VoiceCallState.Connected, hangupCause: null);
        var harness = new Harness(interaction, session, ActivitySources.Manual);

        // Act
        await harness.Handler.HandleAsync(CreateEvent(ContactCenterConstants.Events.AgentLegAnswered), TestContext.Current.CancellationToken);

        // Assert
        harness.Client.Verify(client => client.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    private static Interaction CreateDialerInteraction(InteractionStatus status)
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            CreatedUtc = new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(status);

        interaction.TechnicalMetadata[DialerCallMetadata.DialerProfileIdKey] = "profile-1";

        return interaction;
    }

    private static CallSession CreateSession(VoiceCallState state, HangupCause? hangupCause)
    {
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            Direction = InteractionDirection.Outbound,
            StartedUtc = new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc),
        }.RestorePersistedState(state);

        session.HangupCause = hangupCause;

        return session;
    }

    private static InteractionEvent CreateEvent(string eventType)
        => new()
        {
            EventType = eventType,
            InteractionId = "interaction-1",
        };

    private sealed class Harness
    {
        public Harness(Interaction interaction, CallSession session, string activitySource)
        {
            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByIdAsync(interaction.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync(interaction.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1", UserName = "agent.one" });

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore
                .Setup(store => store.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1", Source = activitySource });

            var clients = new Mock<IHubClients<ITelephonyClient>>();
            clients
                .Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1")))
                .Returns(Client.Object);

            var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
            hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

            Handler = new ContactCenterSoftPhoneEventHandler(
                interactionManager.Object,
                callSessionManager.Object,
                agentManager.Object,
                Store.Object,
                hubContext.Object,
                _shellSettings,
                activityStore.Object,
                NullLogger<ContactCenterSoftPhoneEventHandler>.Instance);
        }

        public Mock<ITelephonyClient> Client { get; } = new();

        public Mock<ITelephonyInteractionStore> Store { get; } = new();

        public ContactCenterSoftPhoneEventHandler Handler { get; }
    }
}

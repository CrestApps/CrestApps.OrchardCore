using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The call leaving the soft phone of the agent it was handed away from: by a transfer, or by a supervisor taking it.
/// </summary>
public sealed class ContactCenterSoftPhoneTransferEventHandlerTests
{
    private static readonly DateTime _takenUtc = new(2026, 9, 25, 18, 10, 43, DateTimeKind.Utc);

    [Fact]
    public async Task ATakeover_EndsTheCallOnTheReleasedAgentsPhone()
    {
        // Arrange: live, the released agent's phone still listed the call after a supervisor took it, and its Hang up
        // ended the call on the customer the supervisor was talking to.
        var fixture = new Fixture();

        // Act
        await fixture.Handler.HandleAsync(Event(ContactCenterConstants.Events.SupervisorTookOver), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_takenUtc, fixture.Projection.EndedUtc);
        Assert.Equal(CallOutcome.Completed, fixture.Projection.Outcome);
        fixture.Store.Verify(store => store.UpdateAsync(fixture.Projection, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(fixture.Pushed);
        Assert.Equal("call-1", fixture.Pushed.CallId);
        Assert.Equal(CallState.Disconnected, fixture.Pushed.State);
        Assert.Equal(true, fixture.Pushed.Metadata["takenOver"]);
    }

    [Fact]
    public async Task ATransfer_StillEndsTheCallOnTheTransferringAgentsPhone()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        await fixture.Handler.HandleAsync(Event(ContactCenterConstants.Events.InteractionTransferred), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(fixture.Projection.EndedUtc);
        Assert.Equal(true, fixture.Pushed.Metadata["transferred"]);
    }

    [Fact]
    public async Task AnyOtherEvent_LeavesThePhoneAlone()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        await fixture.Handler.HandleAsync(Event(ContactCenterConstants.Events.SupervisorMonitorStopped), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(fixture.Projection.EndedUtc);
        Assert.Null(fixture.Pushed);
    }

    private static InteractionEvent Event(string eventType)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = "int-1",
            AggregateId = "int-1",
            OccurredUtc = _takenUtc,
        };

        interactionEvent.SetData(new CallLifecycleEventData { InteractionId = "int-1", AgentId = "agent-1" });

        return interactionEvent;
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            var interactions = new Mock<IInteractionManager>();
            interactions
                .Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Interaction { ItemId = "int-1", ProviderName = "Telnyx", ProviderInteractionId = "call-1" });

            var sessions = new Mock<ICallSessionManager>();
            sessions
                .Setup(manager => manager.FindByInteractionIdAsync("int-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CallSession { InteractionId = "int-1", ProviderCallId = "call-1", ProviderName = "Telnyx" });

            var agents = new Mock<IAgentProfileManager>();
            agents
                .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user" });

            Store
                .Setup(store => store.FindByCallIdAsync("agent-user", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Projection);

            var client = new Mock<ITelephonyClient>();
            client
                .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
                .Callback<TelephonyCall>(call => Pushed = call)
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubClients<ITelephonyClient>>();
            clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser("Default", "agent-user"))).Returns(client.Object);
            var hub = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
            hub.SetupGet(value => value.Clients).Returns(clients.Object);

            Handler = new ContactCenterSoftPhoneTransferEventHandler(
                interactions.Object,
                sessions.Object,
                agents.Object,
                Store.Object,
                hub.Object,
                new StubClock(),
                new ShellSettings { Name = "Default" });
        }

        public TelephonyInteraction Projection { get; } = new()
        {
            UserId = "agent-user",
            CallId = "call-1",
            StartedUtc = _takenUtc.AddMinutes(-3),
        };

        public Mock<ITelephonyInteractionStore> Store { get; } = new();

        public TelephonyCall Pushed { get; private set; }

        public ContactCenterSoftPhoneTransferEventHandler Handler { get; }
    }
}

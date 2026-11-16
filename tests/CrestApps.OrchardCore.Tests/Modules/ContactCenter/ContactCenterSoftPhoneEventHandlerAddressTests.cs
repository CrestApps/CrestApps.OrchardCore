using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Voice Mild (fix verification): the caller id Low cleaned call addresses where voice events are mapped and where the
/// telephony module stores them, but the contact center's soft phone handler still wrote the customer's address to the
/// agent's recent calls as the carrier gave it, on create and on update.
/// </summary>
public sealed class ContactCenterSoftPhoneEventHandlerAddressTests
{
    private const string CraftedCallerId = "+15550001000\"><img src=x onerror=alert(1)>";

    [Fact]
    public async Task HandleAsync_ANewCall_StoresTheCallersAddressCleaned()
    {
        // Arrange
        TelephonyInteraction created = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);
        store.Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => created = interaction)
            .Returns(Task.CompletedTask);

        // Act
        await CreateHandler(store.Object).HandleAsync(ConnectedEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("+155500010001", created.From);
        Assert.Equal("+15550002000", created.To);
    }

    [Fact]
    public async Task HandleAsync_AKnownCall_StoresTheCallersAddressCleaned()
    {
        // Arrange
        var existing = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            UserId = "user-1",
            From = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            Outcome = CallOutcome.InProgress,
        };

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await CreateHandler(store.Object).HandleAsync(ConnectedEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+155500010001", existing.From);
        Assert.Equal("+15550002000", existing.To);
    }

    private static InteractionEvent ConnectedEvent()
        => new()
        {
            EventType = ContactCenterConstants.Events.CallConnected,
            InteractionId = "interaction-1",
        };

    private static ContactCenterSoftPhoneEventHandler CreateHandler(ITelephonyInteractionStore store)
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            CustomerAddress = CraftedCallerId,
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Connected);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            FromAddress = CraftedCallerId,
            ToAddress = "+15550002000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 9, DateTimeKind.Utc),
        }.RestorePersistedState(VoiceCallState.Connected);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1", UserName = "agent.one" });

        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(Mock.Of<ITelephonyClient>());
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        return new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store,
            new SoftPhoneClientNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" }),
            Mock.Of<IOmnichannelActivityStore>(),
            NullLogger<ContactCenterSoftPhoneEventHandler>.Instance);
    }
}

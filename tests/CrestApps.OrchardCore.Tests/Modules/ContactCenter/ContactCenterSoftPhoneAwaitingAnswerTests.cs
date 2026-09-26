using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The soft-phone projection records whether the agent has joined the call, from the agent's own view of it: an offer
/// ringing them is awaiting their answer however live the caller's leg is, and the call they took is not.
/// </summary>
public sealed class ContactCenterSoftPhoneAwaitingAnswerTests
{
    private static readonly ShellSettings _shellSettings = new() { Name = "TenantA" };

    [Fact]
    public async Task AnOfferRingingTheAgent_IsRecordedAsAwaitingTheirAnswer_ThoughTheCallersLegIsLive()
    {
        // Arrange
        // The platform answered the caller's leg for hold music; the agent's phone is still ringing for the offer.
        var interaction = CreateInteraction(InteractionStatus.Ringing);
        var session = CreateSession(VoiceCallState.Connected);
        TelephonyInteraction created = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((entry, _) => created = entry)
            .Returns(Task.CompletedTask);

        // Act
        await CreateHandler(interaction, session, store).HandleAsync(CreateEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.True(created.AwaitingAnswer);
    }

    [Fact]
    public async Task ACallTheAgentTook_IsNoLongerAwaitingTheirAnswer()
    {
        // Arrange
        var interaction = CreateInteraction(InteractionStatus.Connected);
        var session = CreateSession(VoiceCallState.Connected);
        var existing = new TelephonyInteraction
        {
            InteractionId = interaction.ItemId,
            CallId = "call-1",
            UserId = "user-1",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            AwaitingAnswer = true,
        };

        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await CreateHandler(interaction, session, store).HandleAsync(CreateEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(existing.AwaitingAnswer);
        store.Verify(value => value.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Interaction CreateInteraction(InteractionStatus status)
        => new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            CreatedUtc = new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(status);

    private static CallSession CreateSession(VoiceCallState state)
        => new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            Direction = InteractionDirection.Inbound,
            StartedUtc = new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc),
        }.RestorePersistedState(state);

    private static InteractionEvent CreateEvent()
        => new()
        {
            EventType = ContactCenterConstants.Events.CallSessionUpdated,
            InteractionId = "interaction-1",
        };

    private static ContactCenterSoftPhoneEventHandler CreateHandler(
        Interaction interaction,
        CallSession session,
        Mock<ITelephonyInteractionStore> store)
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

        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients
            .Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1")))
            .Returns(new Mock<ITelephonyClient>().Object);

        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        return new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);
    }
}

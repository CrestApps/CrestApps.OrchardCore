using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An offer that ends without an accept settles the offered agent's call-history entry for the call, instead of
/// leaving it "in progress" until the call ends with somebody else.
/// </summary>
public sealed class OfferCallHistorySettlementHandlerTests
{
    private static readonly DateTime _startedUtc = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _settledUtc = _startedUtc.AddSeconds(30);

    [Theory]
    [InlineData(ContactCenterConstants.Events.OfferExpired, CallOutcome.Missed)]
    [InlineData(ContactCenterConstants.Events.OfferCancelled, CallOutcome.Missed)]
    [InlineData(ContactCenterConstants.Events.OfferMissed, CallOutcome.Missed)]
    [InlineData(ContactCenterConstants.Events.OfferDeclined, CallOutcome.Rejected)]
    public async Task AnOfferThatEndsWithoutAnAccept_SettlesTheOfferedAgentsEntry(string eventType, CallOutcome expected)
    {
        // Arrange
        // The call went back to the queue, so it no longer names the agent whose offer ended.
        var interaction = CreateInteraction(agentId: null, InteractionStatus.Created);
        var entry = CreateEntry();
        var (handler, store) = CreateHandler(interaction, entry);

        // Act
        await handler.HandleAsync(CreateOfferEvent(eventType), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, entry.Outcome);
        Assert.Equal(_settledUtc, entry.EndedUtc);
        Assert.Equal(30, entry.DurationSeconds);
        store.Verify(value => value.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEntryAlreadySettled_IsLeftAsItIs()
    {
        // Arrange
        var interaction = CreateInteraction(agentId: null, InteractionStatus.Created);
        var entry = CreateEntry();
        entry.Outcome = CallOutcome.Missed;
        entry.IsVoicemail = true;
        entry.EndedUtc = _startedUtc.AddSeconds(5);
        var (handler, store) = CreateHandler(interaction, entry);

        // Act
        await handler.HandleAsync(CreateOfferEvent(ContactCenterConstants.Events.OfferExpired), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_startedUtc.AddSeconds(5), entry.EndedUtc);
        store.Verify(value => value.UpdateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(InteractionStatus.Ringing)]
    [InlineData(InteractionStatus.Connected)]
    public async Task ACallThatIsWithTheSameAgentAgain_IsNotSettled(InteractionStatus status)
    {
        // Arrange
        // The call was offered to this agent again (or they took it) before the ended offer was delivered here; the
        // entry is the new offer's now.
        var interaction = CreateInteraction(agentId: "agent-1", status);
        var entry = CreateEntry();
        var (handler, store) = CreateHandler(interaction, entry);

        // Act
        await handler.HandleAsync(CreateOfferEvent(ContactCenterConstants.Events.OfferExpired), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.InProgress, entry.Outcome);
        store.Verify(value => value.UpdateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnOfferWithoutItsUser_IsResolvedThroughTheAgent()
    {
        // Arrange
        // An expiry is recorded by the platform, which does not always carry the agent's user on the payload.
        var interaction = CreateInteraction(agentId: null, InteractionStatus.Created);
        var entry = CreateEntry();
        var (handler, _) = CreateHandler(interaction, entry);
        var offerEvent = CreateOfferEvent(ContactCenterConstants.Events.OfferExpired, userId: null);

        // Act
        await handler.HandleAsync(offerEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.Missed, entry.Outcome);
    }

    [Fact]
    public async Task AnAcceptedOffer_DoesNotTouchTheEntry()
    {
        // Arrange
        var interaction = CreateInteraction(agentId: "agent-1", InteractionStatus.Connected);
        var entry = CreateEntry();
        var (handler, store) = CreateHandler(interaction, entry);

        // Act
        await handler.HandleAsync(CreateOfferEvent(ContactCenterConstants.Events.OfferAccepted), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.InProgress, entry.Outcome);
        store.Verify(value => value.FindByCallIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Interaction CreateInteraction(string agentId, InteractionStatus status)
        => new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            AgentId = agentId,
            Direction = InteractionDirection.Inbound,
        }.RestorePersistedStatus(status);

    private static TelephonyInteraction CreateEntry()
        => new()
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "Telnyx",
            UserId = "user-1",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _startedUtc,
        };

    private static InteractionEvent CreateOfferEvent(string eventType, string userId = "user-1")
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = "interaction-1",
            AggregateType = nameof(ActivityReservation),
            AggregateId = "reservation-1",
            OccurredUtc = _settledUtc,
        };

        interactionEvent.SetData(new OfferLifecycleEventData
        {
            ReservationId = "reservation-1",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
            AgentId = "agent-1",
            UserId = userId,
            PresentedUtc = _startedUtc,
            SettledUtc = _settledUtc,
        });

        return interactionEvent;
    }

    private static (OfferCallHistorySettlementHandler Handler, Mock<ITelephonyInteractionStore> Store) CreateHandler(
        Interaction interaction,
        TelephonyInteraction entry)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync(interaction.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new OfferCallHistorySettlementHandler(
            interactionManager.Object,
            new Mock<ICallSessionManager>().Object,
            agentManager.Object,
            store.Object);

        return (handler, store);
    }
}

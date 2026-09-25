using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A call ends once. A rejected call is ended by the reject command, which records the call's end because the
/// provider may never report a hangup for a call it was told to refuse; when it does report one, that hangup is the
/// same end arriving a second time and must not put a second CallEnded on record.
/// </summary>
public sealed class ProviderVoiceEventServiceCallEndedOnceTests
{
    private static readonly DateTime _now = new(2026, 8, 20, 5, 20, 54, DateTimeKind.Utc);

    [Fact]
    public async Task AHangupForACallTheRejectCommandAlreadyEnded_DoesNotPublishASecondCallEnded()
    {
        // Arrange
        var interaction = CreateInteraction(InteractionStatus.Ended);
        var commandEnded = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            IdempotencyKey = "reject-command-1",
        };

        var (service, published) = CreateService(interaction, [commandEnded]);

        // Act
        var session = await service.IngestAsync(CreateHangup(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session);
        Assert.True(CallSessionLifecycle.IsTerminal(session.State));
        Assert.DoesNotContain(published, value => value.EventType == ContactCenterConstants.Events.CallEnded);

        // The session still records what the provider reported; only the duplicate end is dropped.
        Assert.Contains(published, value => value.EventType == ContactCenterConstants.Events.CallSessionUpdated);
    }

    [Fact]
    public async Task AHangupForACallThatWasSettledWithoutARecordedEnd_StillPublishesCallEnded()
    {
        // Arrange
        // Settled, but nothing recorded its end: the hangup is the only CallEnded this call will get.
        var interaction = CreateInteraction(InteractionStatus.Ended);
        var (service, published) = CreateService(interaction, []);

        // Act
        await service.IngestAsync(CreateHangup(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(published, value => value.EventType == ContactCenterConstants.Events.CallEnded);
    }

    [Fact]
    public async Task AHangupForACallThatIsLiveAgain_StillPublishesCallEnded()
    {
        // Arrange
        // An end on record for a call that is live again (a failed action that handed the caller back to routing)
        // is not this end: the call has to be settled already for the hangup to be a repeat.
        var interaction = CreateInteraction(InteractionStatus.Ringing);
        var earlierEnd = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            IdempotencyKey = "earlier-end-1",
        };

        var (service, published) = CreateService(interaction, [earlierEnd]);

        // Act
        await service.IngestAsync(CreateHangup(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(published, value => value.EventType == ContactCenterConstants.Events.CallEnded);
    }

    private static Interaction CreateInteraction(InteractionStatus status)
        => new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            EndedUtc = status == InteractionStatus.Ended ? _now : null,
        }.RestorePersistedStatus(status);

    private static ProviderVoiceEvent CreateHangup()
        => new()
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = VoiceCallState.Ended,
            IdempotencyKey = "hangup-1",
            OccurredUtc = _now.AddMilliseconds(400),
        };

    private static (ProviderVoiceEventService Service, List<InteractionEvent> Published) CreateService(
        Interaction interaction,
        IReadOnlyList<InteractionEvent> history)
    {
        var published = new List<InteractionEvent>();

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession());

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        eventStore
            .Setup(store => store.GetByInteractionAsync(interaction.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => published.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var service = ProviderVoiceEventServiceTests.CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        return (service, published);
    }
}

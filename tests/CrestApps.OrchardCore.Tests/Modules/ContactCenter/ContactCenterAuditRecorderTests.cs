using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The recorder is the one way agent and call state reaches the audit log, so these pin what every record carries:
/// the time the change happened rather than when it was written, the actor, the aggregate a report reads by, and a
/// key that tells a redelivery from a second change a fraction of a second later.
/// </summary>
public sealed class ContactCenterAuditRecorderTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordAgentState_IsDatedByTheTransitionAndNamesTheActor()
    {
        // Arrange
        var (recorder, published) = CreateRecorder();
        var changedUtc = _now.AddMilliseconds(-1234.5678);

        // Act
        await recorder.RecordAgentStateAsync(
            new AgentStateChangedEventData
            {
                AgentId = "agent-1",
                UserId = "user-1",
                PreviousState = AgentPresenceStatus.Available,
                CurrentState = AgentPresenceStatus.Reserved,
                Source = AgentStateChangeSources.Reserved,
                ReservationId = "reservation-1",
                InteractionId = "interaction-1",
                ChangedUtc = changedUtc,
            },
            ContactCenterActor.System,
            TestContext.Current.CancellationToken);

        // Assert
        var interactionEvent = Assert.Single(published);
        Assert.Equal(ContactCenterConstants.Events.AgentStateChanged, interactionEvent.EventType);
        Assert.Equal(nameof(AgentProfile), interactionEvent.AggregateType);
        Assert.Equal("agent-1", interactionEvent.AggregateId);
        Assert.Equal("interaction-1", interactionEvent.InteractionId);
        Assert.Equal(changedUtc, interactionEvent.OccurredUtc);
        Assert.Equal(ContactCenterActorType.System, interactionEvent.ActorType);

        var data = interactionEvent.GetData<AgentStateChangedEventData>();
        Assert.Equal(AgentPresenceStatus.Reserved, data.CurrentState);
        Assert.Equal(changedUtc, data.ChangedUtc);
    }

    [Fact]
    public async Task RecordAgentState_TwoChangesAFractionOfASecondApart_GetTwoKeys()
    {
        // Arrange
        var (recorder, published) = CreateRecorder();

        // Act
        foreach (var offset in new[] { 0.0, 0.3 })
        {
            await recorder.RecordAgentStateAsync(
                new AgentStateChangedEventData
                {
                    AgentId = "agent-1",
                    PreviousState = AgentPresenceStatus.Available,
                    CurrentState = AgentPresenceStatus.Reserved,
                    ChangedUtc = _now.AddMilliseconds(offset),
                },
                ContactCenterActor.System,
                TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(2, published.Select(interactionEvent => interactionEvent.IdempotencyKey).Distinct().Count());
    }

    [Fact]
    public async Task RecordOffer_SettledOffer_CarriesItsRingTimeAndInteraction()
    {
        // Arrange
        var (recorder, published) = CreateRecorder();

        // Act
        await recorder.RecordOfferAsync(
            ContactCenterConstants.Events.OfferExpired,
            new OfferLifecycleEventData
            {
                ReservationId = "reservation-1",
                InteractionId = "interaction-1",
                PresentedUtc = _now.AddSeconds(-20),
                SettledUtc = _now,
                Reason = "Expired",
            },
            ContactCenterActor.System,
            TestContext.Current.CancellationToken);

        // Assert
        var interactionEvent = Assert.Single(published);
        Assert.Equal("interaction-1", interactionEvent.InteractionId);
        Assert.Equal(_now, interactionEvent.OccurredUtc);
        Assert.Equal(20, interactionEvent.GetData<OfferLifecycleEventData>().RingSeconds);
    }

    [Fact]
    public async Task RecordCall_ALongLegId_FitsTheKeyColumn()
    {
        // Arrange
        var (recorder, published) = CreateRecorder();

        // Act
        await recorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegAnswered,
            new CallLifecycleEventData
            {
                InteractionId = "interaction-1",
                ProviderLegId = "v3:" + new string('x', 200),
                State = "Connected",
            },
            _now,
            ContactCenterActor.Provider("Telnyx"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var interactionEvent = Assert.Single(published);
        Assert.True(interactionEvent.IdempotencyKey.Length <= 128);
        Assert.Equal(nameof(Interaction), interactionEvent.AggregateType);
        Assert.Equal(ContactCenterActorType.Provider, interactionEvent.ActorType);
        Assert.Equal("Telnyx", interactionEvent.ActorId);
    }

    private static (ContactCenterAuditRecorder Recorder, List<InteractionEvent> Published) CreateRecorder()
    {
        var published = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => published.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return (new ContactCenterAuditRecorder(publisher.Object, clock.Object), published);
    }
}

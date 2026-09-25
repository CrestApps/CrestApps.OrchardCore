using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// One poor call is noise and a run of them is a headset or a network that will keep failing. These pin where the
/// line is drawn, that the customer's side never counts against the agent, and that a call measured by both the soft
/// phone and the provider counts once.
/// </summary>
public sealed class CallQualityAlertServiceTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Evaluate_ThirdPoorCallOfTheLastFive_RaisesOneAlertNamingTheCause()
    {
        // Arrange
        var (service, publisher) = CreateService(
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, lossPercent: 8),
            Record("c2", CallQualityRating.Good, minutesAgo: 10),
            Record("c3", CallQualityRating.Poor, minutesAgo: 15, lossPercent: 6));

        // Act
        await service.EvaluateAsync(Record("c4", CallQualityRating.Poor, minutesAgo: 0, lossPercent: 9), TestContext.Current.CancellationToken);

        // Assert
        var published = Assert.Single(publisher.Invocations);
        var interactionEvent = Assert.IsType<InteractionEvent>(published.Arguments[0]);
        var alert = interactionEvent.GetData<CallQualityAlertNotification>();

        Assert.Equal(ContactCenterConstants.Events.CallQualityAlertRaised, interactionEvent.EventType);
        Assert.Equal("agent-1", interactionEvent.AggregateId);
        Assert.StartsWith("call-quality-alert:agent-1:", interactionEvent.IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(3, alert.PoorCallCount);
        Assert.Equal(4, alert.RecentCallCount);
        Assert.Equal(nameof(CallQualityCause.PacketLoss), alert.LikelyCause);
    }

    [Fact]
    public async Task Evaluate_SecondPoorCall_RaisesNothing()
    {
        // Arrange
        var (service, publisher) = CreateService(
            Record("c1", CallQualityRating.Poor, minutesAgo: 5),
            Record("c2", CallQualityRating.Good, minutesAgo: 10));

        // Act
        await service.EvaluateAsync(Record("c3", CallQualityRating.Poor, minutesAgo: 0), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public async Task Evaluate_PoorCustomerLegs_NeverCountAgainstTheAgent()
    {
        // Arrange
        var (service, publisher) = CreateService(
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, source: CallQualitySource.Provider, role: CallPartyRole.Customer),
            Record("c2", CallQualityRating.Poor, minutesAgo: 10, source: CallQualitySource.Provider, role: CallPartyRole.Customer));

        // Act
        await service.EvaluateAsync(Record("c3", CallQualityRating.Poor, minutesAgo: 0), TestContext.Current.CancellationToken);
        await service.EvaluateAsync(
            Record("c4", CallQualityRating.Poor, minutesAgo: 0, source: CallQualitySource.Provider, role: CallPartyRole.Customer),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public async Task Evaluate_OneCallMeasuredByBothSources_CountsOnce()
    {
        // Arrange: the same two calls, each measured by the soft phone and by the provider on the agent's leg.
        var (service, publisher) = CreateService(
            Record("c1", CallQualityRating.Poor, minutesAgo: 5),
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, source: CallQualitySource.Provider, role: CallPartyRole.Agent),
            Record("c2", CallQualityRating.Poor, minutesAgo: 10, source: CallQualitySource.Provider, role: CallPartyRole.Agent));

        // Act
        await service.EvaluateAsync(
            Record("c2", CallQualityRating.Poor, minutesAgo: 10),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public async Task Evaluate_PoorLegsOfCallsNoAgentTalkedOn_NeverCountAgainstTheAgent()
    {
        // Arrange: the agent's recent poor legs belong to calls sent to voicemail (by event and by flag), a caller who
        // hung up before anybody answered, and a call only the AI voice agent handled. Only the last call, a genuinely
        // poor one the agent answered (live: soft phone MOS 2.72 with an 892 ms round trip), was a conversation.
        var (service, publisher) = CreateService(
            [
                Voicemail("vm-event"),
                Voicemail("vm-flag", flagged: true),
                Unanswered("abandoned"),
                Answered("ai-only", agentId: null),
                Answered("answered"),
            ],
            [SentToVoicemail("vm-event")],
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "vm-event"),
            Record("c2", CallQualityRating.Poor, minutesAgo: 10, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "vm-flag"),
            Record("c3", CallQualityRating.Poor, minutesAgo: 15, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "abandoned"),
            Record("c4", CallQualityRating.Poor, minutesAgo: 20, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "ai-only"));

        // Act
        await service.EvaluateAsync(
            Record("c5", CallQualityRating.Poor, minutesAgo: 0, interactionId: "answered", roundTripMs: 892),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Theory]
    [InlineData("sent to voicemail")]
    [InlineData("flagged as voicemail")]
    [InlineData("abandoned while ringing")]
    [InlineData("handled by the AI agent alone")]
    public async Task Evaluate_APoorLegOfACallNoAgentTalkedOn_RaisesNothingItself(string call)
    {
        // Arrange: poor conversations already on record, enough for an alert; the newest poor leg belongs to a call no
        // agent talked on, so it is not the call an alert is raised on.
        (Interaction Interaction, InteractionEvent[] Events) setup = call switch
        {
            "sent to voicemail" => (Voicemail("other"), [SentToVoicemail("other")]),
            "flagged as voicemail" => (Voicemail("other", flagged: true), []),
            "abandoned while ringing" => (Unanswered("other"), []),
            _ => (Answered("other", agentId: null), []),
        };

        var (service, publisher) = CreateService(
            [Answered("answered-1"), Answered("answered-2"), Answered("answered-3"), setup.Interaction],
            setup.Events,
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, interactionId: "answered-1"),
            Record("c2", CallQualityRating.Poor, minutesAgo: 10, interactionId: "answered-2"),
            Record("c0", CallQualityRating.Poor, minutesAgo: 15, interactionId: "answered-3"));

        // Act
        await service.EvaluateAsync(
            Record("c3", CallQualityRating.Poor, minutesAgo: 0, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "other"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public async Task Evaluate_ThreePoorAnsweredConversations_RaiseAnAlert()
    {
        // Arrange: the same shape as the calls no agent talked on, with the third call answered by the agent.
        var (service, publisher) = CreateService(
            [Answered("answered-1"), Answered("answered-2"), Answered("answered-3")],
            [],
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, interactionId: "answered-1"),
            Record("c2", CallQualityRating.Poor, minutesAgo: 10, interactionId: "answered-2"));

        // Act
        await service.EvaluateAsync(
            Record("c3", CallQualityRating.Poor, minutesAgo: 0, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "answered-3"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(publisher.Invocations);
    }

    [Fact]
    public async Task Evaluate_RepeatedPoorConversations_RaiseAnAlertCountingOnlyTheConversations()
    {
        // Arrange
        var (service, publisher) = CreateService(
            [Answered("answered-1"), Answered("answered-2"), Answered("answered-3"), Answered("answered-4"), Voicemail("vm-1"), Voicemail("vm-2")],
            [SentToVoicemail("vm-1"), SentToVoicemail("vm-2")],
            Record("c1", CallQualityRating.Poor, minutesAgo: 5, interactionId: "answered-1", roundTripMs: 700),
            Record("c2", CallQualityRating.Poor, minutesAgo: 6, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "vm-1"),
            Record("c3", CallQualityRating.Poor, minutesAgo: 7, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: "vm-2"),
            Record("c4", CallQualityRating.Good, minutesAgo: 8, interactionId: "answered-2"),
            Record("c5", CallQualityRating.Poor, minutesAgo: 9, interactionId: "answered-3", roundTripMs: 450));

        // Act
        await service.EvaluateAsync(
            Record("c6", CallQualityRating.Poor, minutesAgo: 0, interactionId: "answered-4", roundTripMs: 892),
            TestContext.Current.CancellationToken);

        // Assert
        var published = Assert.Single(publisher.Invocations);
        var alert = Assert.IsType<InteractionEvent>(published.Arguments[0]).GetData<CallQualityAlertNotification>();

        Assert.Equal(3, alert.PoorCallCount);
        Assert.Equal(4, alert.RecentCallCount);
        Assert.Equal(nameof(CallQualityCause.Latency), alert.LikelyCause);
    }

    [Fact]
    public async Task Evaluate_StoredProviderAgentLegsRatedOnSkippedPackets_AreReadAsTheyRateNow()
    {
        // Arrange: live agent legs of answered calls, stored as 100% loss when skipped packets were read as loss. The
        // provider received no packets at all from the soft phone on them, and scored what it had at MOS 4.5.
        var (service, publisher) = CreateService(
            [Answered("answered-1"), Answered("answered-2"), Answered("answered-3")],
            [],
            ProviderAgentLeg("c1", minutesAgo: 3, interactionId: "answered-1", inbound: 0, skipped: 596, outbound: 572),
            ProviderAgentLeg("c2", minutesAgo: 4, interactionId: "answered-2", inbound: 0, skipped: 346, outbound: 298));

        // Act: the newest leg is a genuinely poor soft phone measurement, so the alert is evaluated.
        await service.EvaluateAsync(
            Record("c3", CallQualityRating.Poor, minutesAgo: 0, interactionId: "answered-3", roundTripMs: 892),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public void ClassifyMostLikely_PicksTheCommonestKnownCause()
    {
        // Arrange
        var records = new[]
        {
            Record("c1", CallQualityRating.Poor, minutesAgo: 0, jitterMs: 45),
            Record("c2", CallQualityRating.Poor, minutesAgo: 0, jitterMs: 60),
            Record("c3", CallQualityRating.Poor, minutesAgo: 0, lossPercent: 12),
            Record("c4", CallQualityRating.Poor, minutesAgo: 0),
        };

        // Act
        var cause = CallQualityCauseClassifier.ClassifyMostLikely(records);

        // Assert
        Assert.Equal(CallQualityCause.Jitter, cause);
    }

    [Fact]
    public void Classify_NoAudioReceived_OutranksEveryNetworkFigure()
    {
        // Arrange
        var record = Record("c1", CallQualityRating.Poor, minutesAgo: 0, lossPercent: 50);
        record.Browser = new CallQualityReport { PacketsReceived = 400, BytesReceived = 0 };

        // Act
        var cause = CallQualityCauseClassifier.Classify(record);

        // Assert
        Assert.Equal(CallQualityCause.NoAudioReceived, cause);
    }

    private static (CallQualityAlertService Service, Mock<IContactCenterEventPublisher> Publisher) CreateService(params CallQualityRecord[] earlier)
        => CreateService([], [], earlier);

    private static (CallQualityAlertService Service, Mock<IContactCenterEventPublisher> Publisher) CreateService(
        Interaction[] interactions,
        InteractionEvent[] events,
        params CallQualityRecord[] earlier)
    {
        var recordStore = new Mock<ICallQualityRecordStore>();
        recordStore
            .Setup(store => store.GetRecentForAgentAsync("agent-1", It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(earlier.OrderByDescending(record => record.ObservedUtc).ToArray());

        var interactionStore = new Mock<IInteractionStore>();
        interactionStore
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> ids, CancellationToken _) =>
                ValueTask.FromResult<IReadOnlyCollection<Interaction>>(interactions.Where(interaction => ids.Contains(interaction.ItemId)).ToArray()));

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var publisher = new Mock<IContactCenterEventPublisher>();

        return (new CallQualityAlertService(recordStore.Object, interactionStore.Object, eventStore.Object, publisher.Object), publisher);
    }

    private static Interaction Answered(string interactionId, string agentId = "agent-1")
    {
        var interaction = Call(interactionId);
        interaction.AgentId = agentId;
        interaction.AnsweredUtc = _now.AddMinutes(-30);

        return interaction;
    }

    // The platform answers a caller it sends to voicemail, to play the greeting and record the message.
    private static Interaction Voicemail(string interactionId, bool flagged = false)
    {
        var interaction = Call(interactionId);
        interaction.AnsweredUtc = _now.AddMinutes(-30);

        if (flagged)
        {
            interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;
        }

        return interaction;
    }

    private static Interaction Unanswered(string interactionId)
    {
        var interaction = Call(interactionId);
        interaction.TransitionTo(InteractionStatus.Ended);
        interaction.EndedUtc = _now.AddMinutes(-29);

        return interaction;
    }

    // Every call names the agent it was offered to, so only its outcome can tell a conversation from a call that was not.
    private static Interaction Call(string interactionId)
        => new()
        {
            ItemId = interactionId,
            AgentId = "agent-1",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            CreatedUtc = _now.AddMinutes(-31),
        };

    private static InteractionEvent SentToVoicemail(string interactionId)
        => new()
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = interactionId,
            AggregateType = nameof(Interaction),
            AggregateId = interactionId,
            OccurredUtc = _now.AddMinutes(-30),
        };

    private static CallQualityRecord ProviderAgentLeg(string callControlId, int minutesAgo, string interactionId, long inbound, long skipped, long outbound)
    {
        var record = Record(callControlId, CallQualityRating.Poor, minutesAgo, lossPercent: 100, source: CallQualitySource.Provider, role: CallPartyRole.Agent, interactionId: interactionId);
        record.Mos = 4.5;
        record.Provider = new ProviderCallQualityStats
        {
            InboundMos = 4.5,
            InboundJitterMaxVarianceMs = 0,
            InboundPacketCount = inbound,
            InboundSkipPacketCount = skipped,
            OutboundPacketCount = outbound,
            OutboundSkipPacketCount = 0,
        };

        return record;
    }

    private static CallQualityRecord Record(
        string callControlId,
        CallQualityRating rating,
        int minutesAgo,
        double? lossPercent = null,
        double? jitterMs = null,
        CallQualitySource source = CallQualitySource.Browser,
        CallPartyRole role = CallPartyRole.Agent,
        string interactionId = null,
        double? roundTripMs = null)
        => new()
        {
            ItemId = $"{source}-{callControlId}",
            AgentId = "agent-1",
            InteractionId = interactionId,
            Source = source,
            LegRole = role,
            Rating = rating,
            ProviderCallControlId = callControlId,
            LossPercent = lossPercent,
            JitterMs = jitterMs,
            RoundTripMs = roundTripMs,
            ObservedUtc = _now.AddMinutes(-minutesAgo),
        };
}

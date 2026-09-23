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
    {
        var recordStore = new Mock<ICallQualityRecordStore>();
        recordStore
            .Setup(store => store.GetRecentForAgentAsync("agent-1", It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(earlier.OrderByDescending(record => record.ObservedUtc).ToArray());

        var publisher = new Mock<IContactCenterEventPublisher>();

        return (new CallQualityAlertService(recordStore.Object, publisher.Object), publisher);
    }

    private static CallQualityRecord Record(
        string callControlId,
        CallQualityRating rating,
        int minutesAgo,
        double? lossPercent = null,
        double? jitterMs = null,
        CallQualitySource source = CallQualitySource.Browser,
        CallPartyRole role = CallPartyRole.Agent)
        => new()
        {
            ItemId = $"{source}-{callControlId}",
            AgentId = "agent-1",
            Source = source,
            LegRole = role,
            Rating = rating,
            ProviderCallControlId = callControlId,
            LossPercent = lossPercent,
            JitterMs = jitterMs,
            ObservedUtc = _now.AddMinutes(-minutesAgo),
        };
}

using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What a supervisor is told about why queued work is not being handed to agents. The campaign reasons are the decisions
/// the queue gate makes, read through the same evaluator; the queue and routing reasons are the ones assignment records.
/// </summary>
public sealed class DialerDispatchDiagnosticsServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_ARecordThatIsNotWaitingInTheCampaignQueue_IsNotQueued()
    {
        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            queueItem: null, CreateProfile(), CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.NotQueued, blocker);
    }

    // A record a user works from their own list is not in the campaign's queue on purpose.
    [Fact]
    public void Evaluate_ARecordAssignedToAUserAndNotQueued_IsAssignedToThem()
    {
        // Arrange
        var activity = CreateActivity();
        activity.AssignedToId = "user-1";

        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            queueItem: null, CreateProfile(), activity, workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.AssignedToUser, blocker);
    }

    [Fact]
    public void Evaluate_ARecordOfATurnedOffProfile_IsHeldByTheProfile()
    {
        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), CreateProfile(enabled: false), CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.ProfileDisabled, blocker);
    }

    [Fact]
    public void Evaluate_ARecordWithNoAttemptsLeft_IsOutOfAttempts()
    {
        // Arrange
        var profile = CreateProfile();
        profile.MaxAttempts = 2;

        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), profile, CreateActivity(attempts: 3), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.AttemptsExhausted, blocker);
    }

    // The queue gate leaves a record whose last call reached an agent to the dial itself, so it is not out of attempts.
    [Fact]
    public void Evaluate_ARecordOutOfAttemptsWhoseLastCallWasAnswered_IsStillDialable()
    {
        // Arrange
        var profile = CreateProfile();
        profile.MaxAttempts = 2;

        var answered = new Interaction { ItemId = "call-1", ActivityItemId = "act-1", EndedUtc = _now.AddHours(-3) };
        DialerCallMetadata.SetOutcome(answered, DialerAttemptOutcomes.Answered);

        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), profile, CreateActivity(attempts: 3), workState: null, answered, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.DialableNow, blocker);
    }

    [Fact]
    public void Evaluate_ARecordScheduledForLater_WaitsUntilItIsDue()
    {
        // Arrange
        var due = _now.AddMinutes(45);

        // Act
        var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), CreateProfile(), CreateActivity(scheduledUtc: due), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.ScheduledForLater, blocker);
        Assert.Equal(due, dueUtc);
    }

    [Fact]
    public void Evaluate_ARecordInsideTheRetryDelay_WaitsForTheDelayToEnd()
    {
        // Arrange
        var profile = CreateProfile();
        profile.RetryDelayMinutes = 60;

        var last = new Interaction { ItemId = "call-1", ActivityItemId = "act-1", EndedUtc = _now.AddMinutes(-20) };

        // Act
        var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), profile, CreateActivity(), workState: null, last, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.RetryCoolDown, blocker);
        Assert.Equal(_now.AddMinutes(40), dueUtc);
    }

    // The gate waits for whichever is later, so the reason given is the one that ends last.
    [Fact]
    public void Evaluate_ARecordBothScheduledAndCoolingDown_IsHeldByWhicheverEndsLater()
    {
        // Arrange
        var profile = CreateProfile();
        profile.RetryDelayMinutes = 60;

        var last = new Interaction { ItemId = "call-1", ActivityItemId = "act-1", EndedUtc = _now.AddMinutes(-20) };

        // Act
        var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), profile, CreateActivity(scheduledUtc: _now.AddHours(3)), workState: null, last, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.ScheduledForLater, blocker);
        Assert.Equal(_now.AddHours(3), dueUtc);
    }

    [Theory]
    [InlineData(DialerSuppressionReason.OutsideCallingWindow, DispatchBlocker.OutsideCallingWindow)]
    [InlineData(DialerSuppressionReason.AbandonmentRateExceeded, DispatchBlocker.AbandonmentRateExceeded)]
    [InlineData(DialerSuppressionReason.ComplianceScreeningUnavailable, DispatchBlocker.ComplianceScreeningUnavailable)]
    [InlineData(DialerSuppressionReason.DoNotCall, DispatchBlocker.DoNotCall)]
    [InlineData(DialerSuppressionReason.NationalDoNotCallRegistry, DispatchBlocker.NationalDoNotCallRegistry)]
    [InlineData(DialerSuppressionReason.NumberNotInService, DispatchBlocker.NumberNotInService)]
    [InlineData(DialerSuppressionReason.NoDestination, DispatchBlocker.NoDestination)]
    public void Evaluate_ARecordTheComplianceGateSuppressed_IsHeldForThatReason(DialerSuppressionReason reason, DispatchBlocker expected)
    {
        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(),
            CreateProfile(),
            CreateActivity(),
            workState: null,
            lastInteraction: null,
            new DialerSuppressionEventData { Reason = reason },
            _now);

        // Assert
        Assert.Equal(expected, blocker);
    }

    // A queue gate decision comes before the compliance gate's: the gate holds the record before it is ever dialed.
    [Fact]
    public void Evaluate_ARecordHeldByTheQueueGateAndSuppressedBefore_IsHeldByTheQueueGate()
    {
        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(),
            CreateProfile(enabled: false),
            CreateActivity(),
            workState: null,
            lastInteraction: null,
            new DialerSuppressionEventData { Reason = DialerSuppressionReason.OutsideCallingWindow },
            _now);

        // Assert
        Assert.Equal(DispatchBlocker.ProfileDisabled, blocker);
    }

    // The gate lets a record with no dialer profile, or a queued callback, through to routing untouched.
    [Fact]
    public void Evaluate_ARecordWithNoDialerProfile_IsDialable()
    {
        // Arrange
        var queueItem = CreateQueueItem();
        queueItem.DialerProfileId = null;

        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            queueItem, profile: null, CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.DialableNow, blocker);
    }

    [Fact]
    public void Evaluate_AQueuedCallback_IsDialable()
    {
        // Arrange
        var queueItem = CreateQueueItem();
        queueItem.DialerProfileId = QueueCallbackDialerProfile.Id;

        // Act
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(
            queueItem, profile: null, CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.DialableNow, blocker);
    }

    // The pacer reads the profile stamped on the item and finds nothing to dial with, so the record is never dialed.
    [Fact]
    public void Evaluate_ARecordStampedWithAProfileThatNoLongerExists_IsHeldBackForIt()
    {
        // Act
        var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), profile: null, CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.ProfileMissing, blocker);
        Assert.Null(dueUtc);
    }

    [Fact]
    public void Evaluate_ADueRecordOfAnEnabledProfile_IsDialableNow()
    {
        // Act
        var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
            CreateQueueItem(), CreateProfile(), CreateActivity(), workState: null, lastInteraction: null, suppression: null, _now);

        // Assert
        Assert.Equal(DispatchBlocker.DialableNow, blocker);
        Assert.Null(dueUtc);
    }

    // The diagnosis is only worth reading if it is the decision the dispatcher acts on: for every state, the queue gate
    // holds a record back exactly when the diagnosis says it is held.
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public async Task Evaluate_AgreesWithTheQueueGate(bool enabled, bool scheduledLater, bool coolingDown, bool heldBack)
    {
        // Arrange
        var profile = CreateProfile(enabled);
        profile.RetryDelayMinutes = 60;

        var activity = CreateActivity(scheduledUtc: scheduledLater ? _now.AddHours(1) : _now.AddHours(-1));
        var last = coolingDown
            ? new Interaction { ItemId = "call-1", ActivityItemId = "act-1", EndedUtc = _now.AddMinutes(-5) }
            : null;
        var queueItem = CreateQueueItem();

        var profileReader = new Mock<IDialerProfileReader>();
        profileReader.Setup(reader => reader.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var interactions = new Mock<IInteractionManager>();
        interactions.Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(last);

        var queueItems = new Mock<IQueueItemManager>();
        queueItems
            .Setup(manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var gate = new QueuedDialerWorkGate(
            profileReader.Object,
            activityManager.Object,
            interactions.Object,
            Mock.Of<IContactCenterWorkStateService>(),
            queueItems.Object,
            Mock.Of<IActivityQueueService>(),
            Mock.Of<IContactCenterScopeExecutor>(),
            NullLogger<QueuedDialerWorkGate>.Instance);

        // Act
        var gateHeldBack = await gate.TryHoldBackAsync(queueItem, _now, TestContext.Current.CancellationToken);
        var (blocker, _) = DialerDispatchDiagnosticsService.Evaluate(queueItem, profile, activity, workState: null, last, suppression: null, _now);

        // Assert
        Assert.Equal(heldBack, gateHeldBack);
        Assert.Equal(heldBack, blocker != DispatchBlocker.DialableNow);
    }

    [Fact]
    public async Task DiagnoseAsync_AQueueThatIsTurnedOff_HoldsEveryWaitingItem()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: false, waiting: 3);

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(DispatchBlocker.QueueDisabled, diagnosis.QueueBlocker);
        Assert.Equal(3, diagnosis.PendingCount);
        Assert.Equal(3, diagnosis.HeldBackCount);
        Assert.Equal(0, diagnosis.DialableNowCount);
        var bucket = Assert.Single(diagnosis.Buckets);
        Assert.Equal(DispatchBlocker.QueueDisabled, bucket.Blocker);
        Assert.Equal(3, bucket.Count);
    }

    [Fact]
    public async Task DiagnoseAsync_AQueueWhoseBusinessHoursAreClosed_HoldsEveryWaitingItem()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 5, businessHoursOpen: false);

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(DispatchBlocker.BusinessHoursClosed, diagnosis.QueueBlocker);
        Assert.Equal(5, diagnosis.HeldBackCount);
    }

    [Fact]
    public async Task DiagnoseAsync_ARunningQueue_HasNothingHoldingItsWorkBack()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 4);

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(diagnosis.QueueBlocker);
        Assert.Equal(4, diagnosis.DialableNowCount);
        Assert.Equal(0, diagnosis.HeldBackCount);
    }

    [Fact]
    public async Task DiagnoseAsync_WhenRoutingLastFoundNoAgent_SaysWhyEachAgentWasTurnedAway()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 2);
        harness.AddRoutingDecision("support", succeeded: false, _now.AddMinutes(-2), "No eligible agent was found.",
            ("agent-1", false, ["Agent is not available."]),
            ("agent-2", false, ["Agent is not available.", "Agent is at capacity."]),
            ("agent-3", false, ["Agent lacks a required skill."]));

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        var finding = Assert.IsType<DispatchRoutingFinding>(diagnosis.RoutingFinding);
        Assert.Equal("No eligible agent was found.", finding.Reason);
        Assert.Equal(3, finding.CandidateCount);
        Assert.Equal(_now.AddMinutes(-2), finding.OccurredUtc);
        Assert.Equal(
            [("Agent is not available.", 2), ("Agent is at capacity.", 1), ("Agent lacks a required skill.", 1)],
            finding.CandidateReasons.Select(reason => (reason.Reason, reason.AgentCount)));
    }

    // Routing decides on every pass: an assignment after a failure means the failure no longer explains anything.
    [Fact]
    public async Task DiagnoseAsync_WhenRoutingAssignedSinceItFailed_ReportsNoFinding()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 2);
        harness.AddRoutingDecision("support", succeeded: false, _now.AddMinutes(-5), "No eligible agent was found.");
        harness.AddRoutingDecision("support", succeeded: true, _now.AddMinutes(-1), null);

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(diagnosis.RoutingFinding);
    }

    [Fact]
    public async Task DiagnoseAsync_ARoutingFailureOfAnotherQueue_IsNotAttributedToThisOne()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 2);
        harness.AddRoutingDecision("billing", succeeded: false, _now.AddMinutes(-1), "No eligible agent was found.");

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(diagnosis.RoutingFinding);
    }

    [Fact]
    public async Task DiagnoseAsync_ReadsOnlyTheRoutingDecisionsOfTheQueuesAskedAbout_LastMadeRecently()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 1);

        // Act
        await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken);

        // Assert
        harness.DecisionLog.Verify(
            log => log.GetRoutingDecisionsSeenSinceAsync(
                It.Is<IReadOnlyCollection<string>>(queueIds => queueIds.SequenceEqual(new[] { "support" })),
                _now - DialerDispatchDiagnosticsService.RoutingLookback,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Routing counts a repeated failure on the event that recorded it first, so the event's own time can be hours old
    // while the failure was made again a minute ago; it still explains the queue now, as of its last time.
    [Fact]
    public async Task DiagnoseAsync_WhenRoutingKeepsRepeatingAFailureFirstMadeLongAgo_ReportsItAsOfTheLastTime()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("support", enabled: true, waiting: 2);
        harness.AddRoutingDecision(
            "support",
            succeeded: false,
            _now.AddHours(-3),
            "No agents are currently available for this queue.",
            lastSeenUtc: _now.AddMinutes(-1),
            occurrenceCount: 180);

        // Act
        var diagnosis = Assert.Single(await harness.CreateService().DiagnoseAsync(["support"], TestContext.Current.CancellationToken));

        // Assert
        var finding = Assert.IsType<DispatchRoutingFinding>(diagnosis.RoutingFinding);
        Assert.Equal("No agents are currently available for this queue.", finding.Reason);
        Assert.Equal(_now.AddMinutes(-1), finding.OccurredUtc);
    }

    [Fact]
    public async Task DiagnoseAsync_WithNoQueues_ReadsNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.CreateService().DiagnoseAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        harness.EventStore.VerifyNoOtherCalls();
        harness.DecisionLog.VerifyNoOtherCalls();
    }

    private static DialerProfile CreateProfile(bool enabled = true)
        => new()
        {
            ItemId = "profile-1",
            Name = "Power profile",
            Mode = DialerMode.Power,
            Enabled = enabled,
            MaxAttempts = 3,
            RetryDelayMinutes = 0,
        };

    private static OmnichannelActivity CreateActivity(int attempts = 0, DateTime? scheduledUtc = null)
        => new()
        {
            ItemId = "act-1",
            Source = ActivitySources.PreviewDial,
            Status = ActivityStatus.NotStated,
            Attempts = attempts,
            ScheduledUtc = scheduledUtc ?? _now.AddHours(-1),
        };

    private static QueueItem CreateQueueItem()
        => new QueueItem
        {
            ItemId = "item-1",
            QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
            ActivityItemId = "act-1",
            DialerProfileId = "profile-1",
            EnqueuedUtc = _now.AddMinutes(-30),
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

    private sealed class Harness
    {
        private readonly Mock<IActivityQueueManager> _queueManager = new();
        private readonly Mock<IQueueItemManager> _queueItemManager = new();
        private readonly Mock<IBusinessHoursService> _businessHours = new();
        private readonly List<InteractionEvent> _routingEvents = [];

        public Harness()
        {
            DecisionLog
                .Setup(log => log.GetRoutingDecisionsSeenSinceAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> queueIds, DateTime sinceUtc, CancellationToken _) => _routingEvents
                    .Where(interactionEvent => queueIds.Contains(interactionEvent.GetData<ActivityRoutingDecisionEventData>().QueueId))
                    .Where(interactionEvent => (interactionEvent.GetData<ActivityRoutingDecisionEventData>().LastSeenUtc ?? interactionEvent.OccurredUtc) >= sinceUtc)
                    .ToArray());
        }

        public Mock<IInteractionEventStore> EventStore { get; } = new();

        public Mock<IDialerDecisionLog> DecisionLog { get; } = new();

        public void AddQueue(string queueId, bool enabled, int waiting, bool businessHoursOpen = true)
        {
            _queueManager
                .Setup(manager => manager.FindByIdAsync(queueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityQueue { ItemId = queueId, Name = queueId, Enabled = enabled, BusinessHoursCalendarId = "calendar-1" });
            _queueItemManager
                .Setup(manager => manager.CountWaitingAsync(queueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(waiting);
            _businessHours
                .Setup(service => service.IsOpenAsync("calendar-1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(businessHoursOpen);
        }

        public void AddRoutingDecision(
            string queueId,
            bool succeeded,
            DateTime occurredUtc,
            string reason,
            params (string AgentId, bool Eligible, string[] Reasons)[] candidates)
            => AddRoutingDecision(queueId, succeeded, occurredUtc, reason, lastSeenUtc: null, occurrenceCount: 1, candidates);

        public void AddRoutingDecision(
            string queueId,
            bool succeeded,
            DateTime occurredUtc,
            string reason,
            DateTime? lastSeenUtc,
            int occurrenceCount,
            params (string AgentId, bool Eligible, string[] Reasons)[] candidates)
        {
            var interactionEvent = new InteractionEvent
            {
                ItemId = $"event-{_routingEvents.Count + 1}",
                EventType = ContactCenterConstants.Events.RoutingDecisionMade,
                AggregateType = nameof(QueueItem),
                AggregateId = $"item-{_routingEvents.Count + 1}",
                OccurredUtc = occurredUtc,
            };

            interactionEvent.SetData(new ActivityRoutingDecisionEventData
            {
                QueueId = queueId,
                Succeeded = succeeded,
                Reason = reason,
                LastSeenUtc = lastSeenUtc,
                OccurrenceCount = occurrenceCount,
                Candidates = candidates
                    .Select(candidate => new ActivityRoutingCandidateDecisionData
                    {
                        AgentId = candidate.AgentId,
                        IsEligible = candidate.Eligible,
                        Reasons = [.. candidate.Reasons],
                    })
                    .ToArray(),
            });

            _routingEvents.Add(interactionEvent);
        }

        public DialerDispatchDiagnosticsService CreateService()
            => new(
                _queueManager.Object,
                _queueItemManager.Object,
                _businessHours.Object,
                Mock.Of<IDialerProfileReader>(),
                Mock.Of<IOmnichannelActivityManager>(),
                Mock.Of<IContactCenterWorkStateStore>(),
                EventStore.Object,
                DecisionLog.Object,
                Mock.Of<ISession>(),
                new StubClock(_now));
    }
}

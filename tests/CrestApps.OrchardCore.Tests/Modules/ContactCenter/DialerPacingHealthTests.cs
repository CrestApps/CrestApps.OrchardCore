using System.Globalization;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A stalled dialer, an idle dialer and a dialer with nobody to call all place no calls. They are told apart by the
/// dialer's heartbeat, the last pacing cycle that ran to the end, so each one reads as a different state on the report,
/// the live dashboard banner and the health check.
/// </summary>
public sealed class DialerPacingHealthTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _stalledAfter = TimeSpan.FromMinutes(5);

    private static readonly string _stalledQueue = ContactCenterConstants.CampaignQueue.CreateId("stalled");
    private static readonly string _idleQueue = ContactCenterConstants.CampaignQueue.CreateId("idle");
    private static readonly string _nothingQueue = ContactCenterConstants.CampaignQueue.CreateId("nothing");

    [Fact]
    public async Task AStalledAnIdleAndANothingToCallDialer_AreThreeDifferentStates()
    {
        // Arrange
        // Stalled: records have waited half an hour and the last cycle that completed was twenty minutes ago.
        // Idle: cycles keep completing and keep finding no agent.
        // Nothing to call: cycles keep completing and find nothing due; the waiting records are scheduled for later.
        var service = CreateService(
            new QueueSetup(_stalledQueue, _now.AddMinutes(-30), Cycle(_stalledQueue, DialerPacingCycleOutcome.Placed, _now.AddMinutes(-20))),
            new QueueSetup(_idleQueue, _now.AddMinutes(-30), Cycle(_idleQueue, DialerPacingCycleOutcome.NoAgents, _now.AddSeconds(-30))),
            new QueueSetup(_nothingQueue, _now.AddHours(-2), Cycle(_nothingQueue, DialerPacingCycleOutcome.NothingDue, _now.AddSeconds(-20))));

        // Act
        var snapshot = await service.EvaluateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialerPacingHealthState.Stalled, StateOf(snapshot, _stalledQueue));
        Assert.Equal(DialerPacingHealthState.Idle, StateOf(snapshot, _idleQueue));
        Assert.Equal(DialerPacingHealthState.NothingToCall, StateOf(snapshot, _nothingQueue));

        // The most serious state leads, so a stalled queue is never hidden behind an idle one.
        Assert.Equal(_stalledQueue, snapshot.Queues[0].QueueId);
        Assert.Equal(DialerPacingHealthState.Stalled, snapshot.State);
        Assert.Equal(TimeSpan.FromMinutes(20), snapshot.Queues[0].TimeWithoutCompletedCycle);
    }

    [Fact]
    public async Task ADialerWhoseCyclesNeverCompleted_IsStalled_OnceWorkHasWaitedPastTheThreshold()
    {
        // Arrange: work arrived ten minutes ago and the only record is a cycle that ran out of its time budget.
        var service = CreateService(
            new QueueSetup(_stalledQueue, _now.AddMinutes(-10), LastCycle: Cycle(_stalledQueue, DialerPacingCycleOutcome.TimeBudgetExceeded, _now.AddMinutes(-1)), LastCompleted: null));

        // Act
        var snapshot = await service.EvaluateAsync(TestContext.Current.CancellationToken);

        // Assert
        var queue = Assert.Single(snapshot.Queues);
        Assert.Equal(DialerPacingHealthState.Stalled, queue.State);
        Assert.Equal(DialerPacingCycleOutcome.TimeBudgetExceeded, queue.LastCycle.Outcome);
        Assert.Null(queue.LastCompletedCycle);
    }

    [Fact]
    public async Task WorkThatJustArrived_IsStarting_NotStalled()
    {
        // Arrange
        var service = CreateService(new QueueSetup(_idleQueue, _now.AddMinutes(-1), LastCycle: null, LastCompleted: null));

        // Act
        var snapshot = await service.EvaluateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialerPacingHealthState.Starting, Assert.Single(snapshot.Queues).State);
    }

    [Fact]
    public async Task NoPacedCampaignWithWorkWaiting_IsNothingToCall()
    {
        // Arrange
        var service = CreateService();

        // Act
        var snapshot = await service.EvaluateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(snapshot.Queues);
        Assert.Equal(DialerPacingHealthState.NothingToCall, snapshot.State);
    }

    [Fact]
    public void TheBanner_ShowsEachStateDifferently()
    {
        // Arrange
        var localizer = new FormattingLocalizer();
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stalled"] = "Renewals",
            ["idle"] = "Win-back",
        };

        // Act
        var stalled = DialerPacingBannerService.Build(Snapshot(Queue(_stalledQueue, DialerPacingHealthState.Stalled, TimeSpan.FromMinutes(12))), localizer, names);
        var idle = DialerPacingBannerService.Build(Snapshot(Queue(_idleQueue, DialerPacingHealthState.Idle, TimeSpan.FromSeconds(30))), localizer, names);
        var nothing = DialerPacingBannerService.Build(Snapshot(), localizer, names);

        // Assert
        Assert.Equal("danger", stalled.Level);
        Assert.Equal(DialerPacingHealthState.Stalled, stalled.State);
        Assert.Contains("due work is waiting but no pacing cycle has completed for 12 minutes", stalled.Message, StringComparison.Ordinal);
        Assert.Contains("Renewals", stalled.Message, StringComparison.Ordinal);

        Assert.Equal("warning", idle.Level);
        Assert.Equal(DialerPacingHealthState.Idle, idle.State);
        Assert.Contains("no agent is available", idle.Message, StringComparison.Ordinal);
        Assert.Contains("Win-back", idle.Message, StringComparison.Ordinal);

        Assert.Equal("secondary", nothing.Level);
        Assert.Equal(DialerPacingHealthState.NothingToCall, nothing.State);
        Assert.StartsWith("Nothing to call", nothing.Message, StringComparison.Ordinal);

        Assert.Equal(3, new[] { stalled.Message, idle.Message, nothing.Message }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheHealthCheck_FailsOnlyForAStalledDialer()
    {
        // Act
        var stalled = ContactCenterDialerPacingHealthCheck.Describe(
            Snapshot(Queue(_stalledQueue, DialerPacingHealthState.Stalled, TimeSpan.FromMinutes(12))),
            HealthStatus.Unhealthy);
        var idle = ContactCenterDialerPacingHealthCheck.Describe(
            Snapshot(Queue(_idleQueue, DialerPacingHealthState.Idle, TimeSpan.FromSeconds(30))),
            HealthStatus.Unhealthy);
        var nothing = ContactCenterDialerPacingHealthCheck.Describe(Snapshot(), HealthStatus.Unhealthy);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, stalled.Status);
        Assert.Equal("Due work is waiting but no pacing cycle has completed for 12 minutes in 1 campaign queue(s).", stalled.Description);
        Assert.Equal(_stalledQueue, (string)stalled.Data["stalled_queue_ids"]);

        Assert.Equal(HealthStatus.Healthy, idle.Status);
        Assert.Equal(1, (int)idle.Data["idle_queues"]);
        Assert.Contains("1 idle with no agent available", idle.Description, StringComparison.Ordinal);

        Assert.Equal(HealthStatus.Healthy, nothing.Status);
        Assert.Equal("No paced campaign has work waiting.", nothing.Description);
    }

    [Fact]
    public async Task TheHealthCheck_ReportsTheConfiguredFailureStatus_WhenTheDialerIsStalled()
    {
        // Arrange
        var service = CreateService(
            new QueueSetup(_stalledQueue, _now.AddMinutes(-30), Cycle(_stalledQueue, DialerPacingCycleOutcome.NoAgents, _now.AddMinutes(-20))));
        var check = new ContactCenterDialerPacingHealthCheck(service);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                ContactCenterConstants.HealthChecks.DialerPacingCheckName,
                check,
                HealthStatus.Degraded,
                [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]),
        };

        // Act
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.StartsWith("Due work is waiting but no pacing cycle has completed for 20 minutes", result.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DialerPacingCycleOutcome.Placed, DialerPacingHealthState.Dialing)]
    [InlineData(DialerPacingCycleOutcome.NoAgents, DialerPacingHealthState.Idle)]
    [InlineData(DialerPacingCycleOutcome.HeldBack, DialerPacingHealthState.HeldBack)]
    [InlineData(DialerPacingCycleOutcome.NothingDue, DialerPacingHealthState.NothingToCall)]
    public void ARecentlyCompletedCycle_DecidesTheState(DialerPacingCycleOutcome outcome, DialerPacingHealthState expected)
    {
        // Act
        var (state, silence) = DialerPacingHealthEvaluator.Evaluate(
            _now,
            _stalledAfter,
            _now.AddHours(-1),
            Cycle(_idleQueue, outcome, _now.AddSeconds(-45)));

        // Assert
        Assert.Equal(expected, state);
        Assert.Equal(TimeSpan.FromSeconds(45), silence);
    }

    private static DialerPacingHealthState StateOf(DialerPacingHealthSnapshot snapshot, string queueId)
        => snapshot.Queues.Single(queue => queue.QueueId == queueId).State;

    private static DialerPacingCycle Cycle(string queueId, DialerPacingCycleOutcome outcome, DateTime completedUtc)
        => new()
        {
            QueueId = queueId,
            DialerProfileId = "dp1",
            Outcome = outcome,
            StartedUtc = completedUtc.AddSeconds(-1),
            FirstStartedUtc = completedUtc.AddSeconds(-1),
            CompletedUtc = completedUtc,
        };

    private static DialerPacingQueueHealth Queue(string queueId, DialerPacingHealthState state, TimeSpan silence)
        => new()
        {
            QueueId = queueId,
            CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(queueId),
            DialerProfileId = "dp1",
            State = state,
            TimeWithoutCompletedCycle = silence,
        };

    private static DialerPacingHealthSnapshot Snapshot(params DialerPacingQueueHealth[] queues)
        => new()
        {
            EvaluatedUtc = _now,
            StalledAfter = _stalledAfter,
            Queues = [.. queues],
        };

    private static DialerPacingHealthService CreateService(params QueueSetup[] queues)
    {
        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.GetWaitingQueueIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues.Select(queue => queue.QueueId).ToArray());
        queueItemStore
            .Setup(store => store.GetHeadWaitingByQueueAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues
                .Select(queue => new QueueItem { QueueId = queue.QueueId, DialerProfileId = "dp1", EnqueuedUtc = queue.WaitingSinceUtc })
                .ToArray());
        queueItemStore
            .Setup(store => store.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues.ToDictionary(queue => queue.QueueId, _ => 3));

        var cycleStore = new Mock<IDialerPacingCycleStore>();

        foreach (var queue in queues)
        {
            queueItemStore
                .Setup(store => store.FindLongestWaitingAsync(queue.QueueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueItem { QueueId = queue.QueueId, EnqueuedUtc = queue.WaitingSinceUtc });
            cycleStore
                .Setup(store => store.FindLatestByQueueAsync(queue.QueueId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(queue.LastCycle);
            cycleStore
                .Setup(store => store.FindLatestByQueueAsync(queue.QueueId, It.IsNotNull<IEnumerable<DialerPacingCycleOutcome>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(queue.LastCompleted);
        }

        var profile = new DialerProfile
        {
            ItemId = "dp1",
            Name = "power",
            Enabled = true,
            Mode = DialerMode.Power,
        };

        var profileReader = new Mock<IDialerProfileReader>();
        profileReader
            .Setup(reader => reader.FindByIdAsync("dp1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(profile));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new DialerPacingHealthService(
            queueItemStore.Object,
            profileReader.Object,
            cycleStore.Object,
            clock.Object,
            Options.Create(new ContactCenterDialerPacingOptions { StalledAfter = _stalledAfter }));
    }

    private sealed record QueueSetup(string QueueId, DateTime WaitingSinceUtc, DialerPacingCycle LastCycle, DialerPacingCycle LastCompleted)
    {
        public QueueSetup(string queueId, DateTime waitingSinceUtc, DialerPacingCycle completedCycle)
            : this(queueId, waitingSinceUtc, completedCycle, completedCycle)
        {
        }
    }

    private sealed class FormattingLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

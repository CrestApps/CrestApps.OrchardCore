using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueueLimitServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AdmitAsync_WhenTheQueueSetsNoSizeLimit_AdmitsWithoutCountingTheQueue()
    {
        // Arrange
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1" };

        // Act
        var decision = await harness.Service.AdmitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueAdmissionOutcome.Admitted, decision.Outcome);
        Assert.Equal("q1", decision.QueueId);
        harness.QueueItemManager.Verify(manager => manager.CountWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueHasRoom_Admits()
    {
        // Arrange
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1", MaxQueueSize = 3, QueueFullAction = QueueMaxWaitAction.Voicemail };
        harness.SetWaiting("q1", 2);

        // Act
        var decision = await harness.Service.AdmitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueAdmissionOutcome.Admitted, decision.Outcome);
        Assert.Equal("q1", decision.QueueId);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueIsFullAndConfiguredForVoicemail_SendsTheCallerToVoicemail()
    {
        // Arrange
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1", MaxQueueSize = 3, QueueFullAction = QueueMaxWaitAction.Voicemail };
        harness.SetWaiting("q1", 3);

        // Act
        var decision = await harness.Service.AdmitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueAdmissionOutcome.Voicemail, decision.Outcome);
        Assert.False(decision.IsQueued);
        Assert.Null(decision.QueueId);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueIsFullAndConfiguredToOverflow_AdmitsToTheFirstOverflowQueueWithRoom()
    {
        // Arrange
        // The first hop is itself full and the second is disabled, so neither can take the caller; the third
        // can. Handing a full queue's overflow to another full queue would only move the problem.
        var harness = new Harness();
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            MaxQueueSize = 3,
            QueueFullAction = QueueMaxWaitAction.Overflow,
            OverflowTargets =
            [
                new QueueOverflowTarget { QueueId = "full", AfterSeconds = 10 },
                new QueueOverflowTarget { QueueId = "closed", AfterSeconds = 20 },
                new QueueOverflowTarget { QueueId = "general", AfterSeconds = 30 },
            ],
        };
        harness.SetWaiting("q1", 3);
        harness.AddQueue(new ActivityQueue { ItemId = "full", Enabled = true, MaxQueueSize = 1, QueueFullAction = QueueMaxWaitAction.Overflow }, waiting: 1);
        harness.AddQueue(new ActivityQueue { ItemId = "closed", Enabled = false }, waiting: 0);
        harness.AddQueue(new ActivityQueue { ItemId = "general", Enabled = true }, waiting: 50);

        // Act
        var decision = await harness.Service.AdmitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueAdmissionOutcome.Overflowed, decision.Outcome);
        Assert.Equal("general", decision.QueueId);
        Assert.True(decision.IsQueued);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueIsFullAndNoOverflowQueueCanTakeTheCaller_AdmitsOverTheLimit()
    {
        // Arrange
        // A size limit spreads load; it must never hang up on a caller because the overflow chain is
        // misconfigured. The maximum-wait action still governs them once they are in.
        var harness = new Harness();
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            MaxQueueSize = 3,
            QueueFullAction = QueueMaxWaitAction.Overflow,
            OverflowQueueId = "missing",
        };
        harness.SetWaiting("q1", 3);

        // Act
        var decision = await harness.Service.AdmitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueAdmissionOutcome.Admitted, decision.Outcome);
        Assert.Equal("q1", decision.QueueId);
    }

    [Fact]
    public async Task EnforceMaxWaitAsync_WhenTheQueueSetsNoMaximum_DoesNotReadTheQueue()
    {
        // Arrange
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1", MaxWaitSeconds = 0, MaxWaitAction = QueueMaxWaitAction.Voicemail };

        // Act
        var applied = await harness.Service.EnforceMaxWaitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, applied);
        harness.QueueItemManager.Verify(manager => manager.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnforceMaxWaitAsync_MeasuresTheWaitFromWhenTheCallerEnteredThisQueue()
    {
        // Arrange
        // The second caller overflowed in a minute ago after a long wait elsewhere. This queue promised to cap
        // its own wait, so only the caller who has been here past the limit is acted on.
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1", MaxWaitSeconds = 120, MaxWaitAction = QueueMaxWaitAction.Voicemail };
        var overdue = new QueueItem { ItemId = "i-overdue", ActivityItemId = "act-overdue", QueueId = "q1", EnqueuedUtc = _now.AddMinutes(-3), QueueEnteredUtc = _now.AddMinutes(-3) };
        var recent = new QueueItem { ItemId = "i-recent", ActivityItemId = "act-recent", QueueId = "q1", EnqueuedUtc = _now.AddMinutes(-10), QueueEnteredUtc = _now.AddMinutes(-1) };
        harness.SetWaiting("q1", overdue, recent);
        harness.VoicemailSink
            .Setup(sink => sink.SendToVoicemailAsync(It.IsAny<QueueItem>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var applied = await harness.Service.EnforceMaxWaitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, applied);
        harness.VoicemailSink.Verify(
            sink => sink.SendToVoicemailAsync(overdue, ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.VoicemailSink.Verify(
            sink => sink.SendToVoicemailAsync(recent, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnforceMaxWaitAsync_WhenConfiguredToOverflow_HandsTheCallerToTheFirstHopTheyHaveNotBeenThrough()
    {
        // Arrange
        // The caller has already been through "sales", so the maximum-wait overflow skips it and takes the next
        // hop, regardless of that hop's own wait threshold: this queue has had its chance to answer.
        var harness = new Harness();
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            MaxWaitSeconds = 60,
            MaxWaitAction = QueueMaxWaitAction.Overflow,
            OverflowTargets =
            [
                new QueueOverflowTarget { QueueId = "sales", AfterSeconds = 30 },
                new QueueOverflowTarget { QueueId = "general", AfterSeconds = 600 },
            ],
        };
        var item = new QueueItem { ItemId = "i1", ActivityItemId = "act1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-90), OverflowHistory = ["sales"] };
        harness.SetWaiting("q1", item);

        // Act
        var applied = await harness.Service.EnforceMaxWaitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, applied);
        harness.QueueService.Verify(service => service.OverflowItemAsync(item, queue, "general", It.IsAny<CancellationToken>()), Times.Once);
        harness.VoicemailSink.Verify(sink => sink.SendToVoicemailAsync(It.IsAny<QueueItem>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnforceMaxWaitAsync_WhenNoVoiceFeatureCanMoveTheCaller_LeavesThemWaiting()
    {
        // Arrange
        // The queues feature alone cannot move a live call. The caller stays where they are, the sweep does
        // not fail, and nothing is counted as applied.
        var harness = new Harness();
        var queue = new ActivityQueue { ItemId = "q1", MaxWaitSeconds = 60, MaxWaitAction = QueueMaxWaitAction.Voicemail };
        var item = new QueueItem { ItemId = "i1", ActivityItemId = "act1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-90) };
        harness.SetWaiting("q1", item);
        harness.VoicemailSink
            .Setup(sink => sink.SendToVoicemailAsync(It.IsAny<QueueItem>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var applied = await harness.Service.EnforceMaxWaitAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, applied);
        harness.QueueService.Verify(
            service => service.OverflowItemAsync(It.IsAny<QueueItem>(), It.IsAny<ActivityQueue>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class Harness
    {
        private readonly Dictionary<string, ActivityQueue> _queues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _waitingCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QueueItem[]> _waitingItems = new(StringComparer.OrdinalIgnoreCase);

        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IActivityQueueManager> QueueManager { get; } = new();

        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IWaitingCallVoicemailSink> VoicemailSink { get; } = new();

        public QueueLimitService Service { get; }

        public Harness()
        {
            QueueItemManager
                .Setup(manager => manager.CountWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _waitingCounts.TryGetValue(queueId, out var count) ? count : 0);

            QueueItemManager
                .Setup(manager => manager.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _waitingItems.TryGetValue(queueId, out var items) ? items : []);

            QueueManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _queues.TryGetValue(queueId, out var queue) ? queue : null);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            Service = new QueueLimitService(
                QueueItemManager.Object,
                QueueManager.Object,
                QueueService.Object,
                VoicemailSink.Object,
                clock.Object,
                NullLogger<QueueLimitService>.Instance);
        }

        public void AddQueue(ActivityQueue queue, int waiting)
        {
            _queues[queue.ItemId] = queue;
            _waitingCounts[queue.ItemId] = waiting;
        }

        public void SetWaiting(string queueId, int count)
            => _waitingCounts[queueId] = count;

        public void SetWaiting(string queueId, params QueueItem[] items)
        {
            _waitingItems[queueId] = items;
            _waitingCounts[queueId] = items.Length;
        }
    }
}

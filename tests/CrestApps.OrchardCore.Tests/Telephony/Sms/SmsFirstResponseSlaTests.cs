using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// A customer who texts a queue and gets no reply has no way to tell whether they were heard. Nothing measured
/// that: there was no first-response timer, no escalation when one expired, and so no way for a supervisor to
/// find the threads going unanswered before the customer gave up. These pin the timer and what happens when it
/// runs out.
/// </summary>
public sealed class SmsFirstResponseSlaTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Placing_AThreadOnAQueueWithATarget_SetsTheFirstResponseDeadline()
    {
        // Arrange
        var conversation = new SmsConversation { ItemId = "c1", OwnerId = "queue-1", OwnerType = SmsConversationOwnerType.Queue };
        var service = CreateService(firstResponseTargetSeconds: 300);

        // Act
        await service.ApplyFirstResponseTargetAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddSeconds(300), conversation.FirstResponseDueUtc);
    }

    [Fact]
    public async Task Placing_AThreadOnAQueueWithNoTarget_LeavesNoDeadline()
    {
        // Arrange
        // A queue that has not chosen a target has not opted into the SLA, and inventing one would flood the
        // supervisor view with breaches nobody agreed to.
        var conversation = new SmsConversation { ItemId = "c1", OwnerId = "queue-1", OwnerType = SmsConversationOwnerType.Queue };
        var service = CreateService(firstResponseTargetSeconds: 0);

        // Act
        await service.ApplyFirstResponseTargetAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(conversation.FirstResponseDueUtc);
    }

    [Fact]
    public async Task Placing_AThreadThatAlreadyHasADeadline_DoesNotPushItOut()
    {
        // Arrange
        // The clock starts when the customer first waited. Restarting it on every re-placement would mean a
        // thread bounced between agents never breaches, which is exactly the thread that has.
        var existing = _now.AddSeconds(-60);
        var conversation = new SmsConversation
        {
            ItemId = "c1",
            OwnerId = "queue-1",
            OwnerType = SmsConversationOwnerType.Queue,
            FirstResponseDueUtc = existing,
        };

        var service = CreateService(firstResponseTargetSeconds: 300);

        // Act
        await service.ApplyFirstResponseTargetAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(existing, conversation.FirstResponseDueUtc);
    }

    [Fact]
    public async Task Replying_ClearsTheDeadline()
    {
        // Arrange
        var conversation = new SmsConversation
        {
            ItemId = "c1",
            OwnerId = "queue-1",
            FirstResponseDueUtc = _now.AddSeconds(120),
        };

        var service = CreateService(firstResponseTargetSeconds: 300);

        // Act
        service.MarkResponded(conversation);

        // Assert
        Assert.Null(conversation.FirstResponseDueUtc);
        Assert.Equal(_now, conversation.FirstRespondedUtc);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Sweep_EscalatesOnlyTheThreadsWhoseDeadlineHasPassed()
    {
        // Arrange
        var breached = new SmsConversation
        {
            ItemId = "breached",
            OwnerId = "queue-1",
            OwnerType = SmsConversationOwnerType.Queue,
            Status = SmsConversationStatus.Open,
            FirstResponseDueUtc = _now.AddSeconds(-1),
        };

        var stillInTime = new SmsConversation
        {
            ItemId = "in-time",
            OwnerId = "queue-1",
            OwnerType = SmsConversationOwnerType.Queue,
            Status = SmsConversationStatus.Open,
            FirstResponseDueUtc = _now.AddSeconds(60),
        };

        var store = new Mock<ISmsConversationStore>();
        store.Setup(value => value.GetFirstResponseOverdueAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync([breached]);
        store.Setup(value => value.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var notifier = new Mock<ISmsRealTimeNotifier>();
        var service = CreateService(firstResponseTargetSeconds: 300, store: store, notifier: notifier);

        // Act
        var escalated = await service.EscalateOverdueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, escalated);
        Assert.True(breached.FirstResponseBreached);
        Assert.False(stillInTime.FirstResponseBreached);
        notifier.Verify(
            value => value.FirstResponseBreachedAsync(It.Is<SmsFirstResponseBreachNotification>(n => n.ConversationId == "breached"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sweep_EscalatesEachThreadOnlyOnce()
    {
        // Arrange
        // The sweep runs every thirty seconds. Re-announcing the same breach every pass turns a signal that a
        // customer is waiting into noise a supervisor learns to ignore.
        var breached = new SmsConversation
        {
            ItemId = "breached",
            OwnerId = "queue-1",
            Status = SmsConversationStatus.Open,
            FirstResponseDueUtc = _now.AddSeconds(-1),
            FirstResponseBreached = true,
        };

        var store = new Mock<ISmsConversationStore>();
        store.Setup(value => value.GetFirstResponseOverdueAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync([breached]);

        var notifier = new Mock<ISmsRealTimeNotifier>();
        var service = CreateService(firstResponseTargetSeconds: 300, store: store, notifier: notifier);

        // Act
        var escalated = await service.EscalateOverdueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, escalated);
        notifier.Verify(
            value => value.FirstResponseBreachedAsync(It.IsAny<SmsFirstResponseBreachNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static SmsFirstResponseSlaService CreateService(
        int firstResponseTargetSeconds,
        Mock<ISmsConversationStore> store = null,
        Mock<ISmsRealTimeNotifier> notifier = null)
    {
        var queuePolicyReader = new Mock<ISmsQueuePolicyReader>();
        queuePolicyReader.Setup(reader => reader.ReadAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsQueuePolicy(true, firstResponseTargetSeconds, null));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new SmsFirstResponseSlaService(
            (store ?? new Mock<ISmsConversationStore>()).Object,
            queuePolicyReader.Object,
            (notifier ?? new Mock<ISmsRealTimeNotifier>()).Object,
            clock.Object,
            NullLogger<SmsFirstResponseSlaService>.Instance);
    }
}

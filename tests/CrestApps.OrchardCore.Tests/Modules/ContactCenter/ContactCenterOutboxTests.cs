using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterOutboxTests
{
    private static readonly DateTime _now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchAsync_WhenAllHandlersSucceed_DoesNotScheduleRetry()
    {
        // Arrange
        var handler = new Mock<IContactCenterEventHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outboxStore = new Mock<IContactCenterOutboxStore>();
        var outbox = CreateOutbox(outboxStore, new Mock<IInteractionEventStore>(), [handler.Object]);

        var interactionEvent = new InteractionEvent { ItemId = "e1", EventType = ContactCenterConstants.Events.InteractionCreated };

        // Act
        await outbox.DispatchAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        outboxStore.Verify(s => s.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerFails_SchedulesAPendingRetry_AndStillRunsOtherHandlers()
    {
        // Arrange
        var faulty = new Mock<IContactCenterEventHandler>();
        faulty.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        var healthy = new Mock<IContactCenterEventHandler>();
        healthy.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outboxStore = new Mock<IContactCenterOutboxStore>();
        ContactCenterOutboxMessage created = null;
        outboxStore
            .Setup(s => s.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterOutboxMessage, CancellationToken>((m, _) => created = m)
            .Returns(ValueTask.CompletedTask);

        var outbox = CreateOutbox(outboxStore, new Mock<IInteractionEventStore>(), [faulty.Object, healthy.Object]);

        var interactionEvent = new InteractionEvent { ItemId = "e1", EventType = ContactCenterConstants.Events.InteractionCreated };

        // Act
        await outbox.DispatchAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        healthy.Verify(h => h.HandleAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(created);
        Assert.Equal("e1", created.EventId);
        Assert.Equal(OutboxMessageStatus.Pending, created.Status);
        Assert.Equal(1, created.AttemptCount);
        Assert.Equal(_now.AddSeconds(30), created.NextAttemptUtc);
        Assert.Equal("boom", created.LastError);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenRetrySucceeds_DeletesTheMessage()
    {
        // Arrange
        var message = new ContactCenterOutboxMessage { ItemId = "m1", EventId = "e1", Status = OutboxMessageStatus.Pending, AttemptCount = 1 };
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore.Setup(s => s.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(s => s.FindByIdAsync("e1", It.IsAny<CancellationToken>())).ReturnsAsync(new InteractionEvent { ItemId = "e1" });

        var handler = new Mock<IContactCenterEventHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outbox = CreateOutbox(outboxStore, eventStore, [handler.Object]);

        // Act
        var redelivered = await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, redelivered);
        outboxStore.Verify(s => s.DeleteAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenRetryFails_IncrementsAttemptAndAppliesBackoff()
    {
        // Arrange
        var message = new ContactCenterOutboxMessage { ItemId = "m1", EventId = "e1", Status = OutboxMessageStatus.Pending, AttemptCount = 1 };
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore.Setup(s => s.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(s => s.FindByIdAsync("e1", It.IsAny<CancellationToken>())).ReturnsAsync(new InteractionEvent { ItemId = "e1" });

        var handler = new Mock<IContactCenterEventHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("still failing"));

        var outbox = CreateOutbox(outboxStore, eventStore, [handler.Object]);

        // Act
        var redelivered = await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, redelivered);
        Assert.Equal(2, message.AttemptCount);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(_now.AddSeconds(60), message.NextAttemptUtc);
        outboxStore.Verify(s => s.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenMaxAttemptsReached_DeadLetters()
    {
        // Arrange
        var message = new ContactCenterOutboxMessage
        {
            ItemId = "m1",
            EventId = "e1",
            Status = OutboxMessageStatus.Pending,
            AttemptCount = ContactCenterOutbox.MaxAttempts - 1,
        };

        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore.Setup(s => s.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(s => s.FindByIdAsync("e1", It.IsAny<CancellationToken>())).ReturnsAsync(new InteractionEvent { ItemId = "e1" });

        var handler = new Mock<IContactCenterEventHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("permanent"));

        var outbox = CreateOutbox(outboxStore, eventStore, [handler.Object]);

        // Act
        await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OutboxMessageStatus.DeadLettered, message.Status);
        outboxStore.Verify(s => s.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenEventNoLongerExists_DeadLetters()
    {
        // Arrange
        var message = new ContactCenterOutboxMessage { ItemId = "m1", EventId = "missing", Status = OutboxMessageStatus.Pending, AttemptCount = 1 };
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore.Setup(s => s.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(s => s.FindByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((InteractionEvent)null);

        var outbox = CreateOutbox(outboxStore, eventStore, []);

        // Act
        await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OutboxMessageStatus.DeadLettered, message.Status);
        outboxStore.Verify(s => s.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ContactCenterOutbox CreateOutbox(
        Mock<IContactCenterOutboxStore> outboxStore,
        Mock<IInteractionEventStore> eventStore,
        IEnumerable<IContactCenterEventHandler> handlers)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterOutbox(
            handlers,
            outboxStore.Object,
            eventStore.Object,
            clock.Object,
            NullLogger<ContactCenterOutbox>.Instance);
    }
}

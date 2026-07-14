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
    public async Task EnqueueAsync_CreatesPendingMessageWithoutDispatchingHandlers()
    {
        // Arrange
        var handler = new Mock<IContactCenterEventHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outboxStore = new Mock<IContactCenterOutboxStore>();
        ContactCenterOutboxMessage created = null;
        outboxStore
            .Setup(s => s.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterOutboxMessage, CancellationToken>((message, _) => created = message)
            .Returns(ValueTask.CompletedTask);

        var outbox = CreateOutbox(outboxStore, new Mock<IInteractionEventStore>(), [handler.Object]);

        var interactionEvent = new InteractionEvent { ItemId = "e1", EventType = ContactCenterConstants.Events.InteractionCreated };

        // Act
        await outbox.EnqueueAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("e1", created.EventId);
        Assert.Equal(OutboxMessageStatus.Pending, created.Status);
        Assert.Equal(0, created.AttemptCount);
        Assert.Equal(_now, created.NextAttemptUtc);
        handler.Verify(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenAllHandlersSucceed_RemovesPendingMessage()
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
        outboxStore.Verify(s => s.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(s => s.DeleteAsync(It.Is<ContactCenterOutboxMessage>(message => message.EventId == "e1"), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task DispatchDueAsync_AfterPartialFailure_RetriesOnlyIncompleteHandlers()
    {
        // Arrange
        var completedHandler = new Mock<IContactCenterEventHandler>();
        completedHandler
            .Setup(handler => handler.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var retryingHandler = new Mock<IContactCenterEventHandler>();
        retryingHandler
            .SetupSequence(handler => handler.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient"))
            .Returns(Task.CompletedTask);
        ContactCenterOutboxMessage pending = null;
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore
            .Setup(store => store.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterOutboxMessage, CancellationToken>((message, _) => pending = message)
            .Returns(ValueTask.CompletedTask);
        outboxStore
            .Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [pending]);
        var interactionEvent = new InteractionEvent
        {
            ItemId = "e1",
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.FindByIdAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactionEvent);
        var outbox = CreateOutbox(outboxStore, eventStore, [completedHandler.Object, retryingHandler.Object]);

        // Act
        await outbox.DispatchAsync(interactionEvent, TestContext.Current.CancellationToken);
        var processed = await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, processed);
        completedHandler.Verify(
            handler => handler.HandleAsync(interactionEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        retryingHandler.Verify(
            handler => handler.HandleAsync(interactionEvent, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        outboxStore.Verify(
            store => store.DeleteAsync(pending, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_AfterHandlerOrderChanges_ReplaysCompletedHandlerToday()
    {
        // Arrange
        var firstHandlerRuns = 0;
        var secondHandlerRuns = 0;
        ContactCenterOutboxMessage pending = null;
        var interactionEvent = new InteractionEvent
        {
            ItemId = "e1",
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore
            .Setup(store => store.CreateAsync(It.IsAny<ContactCenterOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterOutboxMessage, CancellationToken>((message, _) => pending = message)
            .Returns(ValueTask.CompletedTask);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.FindByIdAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactionEvent);
        var oldFirstHandler = new FirstTestHandler(() =>
        {
            firstHandlerRuns++;

            return Task.CompletedTask;
        });
        var oldSecondHandler = new SecondTestHandler(() =>
        {
            secondHandlerRuns++;

            return Task.FromException(new InvalidOperationException("transient"));
        });
        var oldVersionOutbox = CreateOutbox(outboxStore, eventStore, [oldFirstHandler, oldSecondHandler]);

        await oldVersionOutbox.DispatchAsync(interactionEvent, TestContext.Current.CancellationToken);
        Assert.Single(pending.CompletedHandlerTypes);
        var persistedCheckpoint = Clone(pending);
        Assert.NotSame(pending, persistedCheckpoint);
        outboxStore
            .Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [Clone(persistedCheckpoint)]);

        var newSecondHandler = new SecondTestHandler(() =>
        {
            secondHandlerRuns++;

            return Task.CompletedTask;
        });
        var newFirstHandler = new FirstTestHandler(() =>
        {
            firstHandlerRuns++;

            return Task.CompletedTask;
        });
        var newVersionOutbox = CreateOutbox(outboxStore, eventStore, [newSecondHandler, newFirstHandler]);

        // Act
        var processed = await newVersionOutbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(2, firstHandlerRuns);
        Assert.Equal(2, secondHandlerRuns);
        outboxStore.Verify(
            store => store.DeleteAsync(
                It.Is<ContactCenterOutboxMessage>(message =>
                    message.EventId == "e1" &&
                    !ReferenceEquals(message, pending)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenFirstDueMessageFails_DoesNotProcessLaterMessageToday()
    {
        // Arrange
        var firstMessage = new ContactCenterOutboxMessage
        {
            ItemId = "m1",
            EventId = "e1",
            Status = OutboxMessageStatus.Pending,
        };
        var secondMessage = new ContactCenterOutboxMessage
        {
            ItemId = "m2",
            EventId = "e2",
            Status = OutboxMessageStatus.Pending,
        };
        var firstEvent = new InteractionEvent { ItemId = "e1" };
        var secondEvent = new InteractionEvent { ItemId = "e2" };
        var outboxStore = new Mock<IContactCenterOutboxStore>();
        outboxStore
            .Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstMessage, secondMessage]);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.FindByIdAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstEvent);
        eventStore
            .Setup(store => store.FindByIdAsync("e2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondEvent);
        var handler = new Mock<IContactCenterEventHandler>();
        handler
            .Setup(service => service.HandleAsync(firstEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("poison"));
        handler
            .Setup(service => service.HandleAsync(secondEvent, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var outbox = CreateOutbox(outboxStore, eventStore, [handler.Object]);

        // Act
        var processed = await outbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(1, firstMessage.AttemptCount);
        Assert.Equal("poison", firstMessage.LastError);
        Assert.Equal(_now.AddSeconds(30), firstMessage.NextAttemptUtc);
        handler.Verify(
            service => service.HandleAsync(firstEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        handler.Verify(
            service => service.HandleAsync(secondEvent, It.IsAny<CancellationToken>()),
            Times.Never);
        outboxStore.Verify(
            store => store.UpdateAsync(firstMessage, It.IsAny<CancellationToken>()),
            Times.Once);
        eventStore.Verify(
            store => store.FindByIdAsync("e2", It.IsAny<CancellationToken>()),
            Times.Never);
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

    private static ContactCenterOutboxMessage Clone(ContactCenterOutboxMessage message)
    {
        return new ContactCenterOutboxMessage
        {
            ItemId = message.ItemId,
            EventId = message.EventId,
            EventType = message.EventType,
            Status = message.Status,
            AttemptCount = message.AttemptCount,
            NextAttemptUtc = message.NextAttemptUtc,
            LastError = message.LastError,
            CompletedHandlerTypes = [.. message.CompletedHandlerTypes],
            CreatedUtc = message.CreatedUtc,
            ModifiedUtc = message.ModifiedUtc,
        };
    }

    private sealed class FirstTestHandler(Func<Task> action) : IContactCenterEventHandler
    {
        public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
        {
            return action();
        }
    }

    private sealed class SecondTestHandler(Func<Task> action) : IContactCenterEventHandler
    {
        public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
        {
            return action();
        }
    }
}

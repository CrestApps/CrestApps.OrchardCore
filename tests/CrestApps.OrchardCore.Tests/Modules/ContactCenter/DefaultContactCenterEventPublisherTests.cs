using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DefaultContactCenterEventPublisherTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PublishAsync_WithoutAmbientShellScope_PersistsForBackgroundDispatchWithoutRunningHandlersInline()
    {
        // Arrange
        var store = CreateStore();
        var outbox = new Mock<IContactCenterOutbox>();
        var publisher = CreatePublisher(store.Object, outbox.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            InteractionId = "interaction-1",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(o => o.EnqueueAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenPostCommitScopeIsAvailable_DefersOutboxDispatch()
    {
        // Arrange
        var store = CreateStore();
        var outbox = new Mock<IContactCenterOutbox>();
        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton(outbox.Object)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<ContactCenterEventDispatchContext>>(
                NullLogger<ContactCenterEventDispatchContext>.Instance)
            .AddTransient<ContactCenterEventDispatchContext>()
            .BuildServiceProvider();
        var scopeExecutor = new TestContactCenterScopeExecutor(services)
        {
            ScheduleAfterCommitResult = true,
        };
        var publisher = CreatePublisher(store.Object, outbox.Object, scopeExecutor);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            InteractionId = "interaction-1",
        };
        store
            .Setup(service => service.FindByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactionEvent);

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        outbox.Verify(
            service => service.DispatchAsync(
                It.IsAny<InteractionEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.NotNull(scopeExecutor.ScheduledOperation);

        await scopeExecutor.ScheduledOperation();

        outbox.Verify(
            service => service.DispatchAsync(
                interactionEvent,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A webhook that connects an agent publishes several events. Each event's handlers ran one after another, so the push
    // that opens the call's record waited for every handler of the events before it.
    [Fact]
    public async Task PublishAsync_PushesEveryEventOfTheChange_BeforeAnyEventsOtherHandlersRun()
    {
        // Arrange
        var first = new InteractionEvent { ItemId = "event-1", EventType = ContactCenterConstants.Events.AgentStateChanged };
        var second = new InteractionEvent { ItemId = "event-2", EventType = ContactCenterConstants.Events.AgentLegAnswered };
        var store = CreateStore();
        store.Setup(s => s.FindByIdAsync("event-1", It.IsAny<CancellationToken>())).ReturnsAsync(first);
        store.Setup(s => s.FindByIdAsync("event-2", It.IsAny<CancellationToken>())).ReturnsAsync(second);

        var calls = new List<string>();
        var outbox = new Mock<IContactCenterOutbox>();
        outbox
            .Setup(o => o.DispatchRealTimeAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => calls.Add("push " + e.ItemId))
            .ReturnsAsync(["Test/Push/v1"]);
        outbox
            .Setup(o => o.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, IReadOnlyCollection<string>, CancellationToken>((e, done, _) => calls.Add("dispatch " + e.ItemId + " skipping " + string.Join(",", done)))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton(outbox.Object)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<ContactCenterEventDispatchContext>>(NullLogger<ContactCenterEventDispatchContext>.Instance)
            .AddTransient<ContactCenterEventDispatchContext>()
            .BuildServiceProvider();
        var scopeExecutor = new TestContactCenterScopeExecutor(services) { ScheduleAfterCommitResult = true };
        var publisher = CreatePublisher(store.Object, outbox.Object, scopeExecutor);

        await publisher.PublishAsync(first, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(second, TestContext.Current.CancellationToken);

        // Act: the post-commit tasks run in the order they were scheduled.
        foreach (var operation in scopeExecutor.ScheduledOperations)
        {
            await operation();
        }

        // Assert
        Assert.Equal(
        [
            "push event-1",
            "push event-2",
            "dispatch event-1 skipping Test/Push/v1",
            "dispatch event-2 skipping Test/Push/v1",
        ], calls);
        outbox.Verify(o => o.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenTheRealTimePassDeliveredNothing_TheEventsOwnDispatchRunsEveryHandler()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { ItemId = "event-1", EventType = ContactCenterConstants.Events.CallConnected };
        var store = CreateStore();
        store.Setup(s => s.FindByIdAsync("event-1", It.IsAny<CancellationToken>())).ReturnsAsync(interactionEvent);
        var outbox = new Mock<IContactCenterOutbox>();
        outbox
            .Setup(o => o.DispatchRealTimeAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton(outbox.Object)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<ContactCenterEventDispatchContext>>(NullLogger<ContactCenterEventDispatchContext>.Instance)
            .AddTransient<ContactCenterEventDispatchContext>()
            .BuildServiceProvider();
        var scopeExecutor = new TestContactCenterScopeExecutor(services) { ScheduleAfterCommitResult = true };
        var publisher = CreatePublisher(store.Object, outbox.Object, scopeExecutor);

        await publisher.PublishAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Act
        foreach (var operation in scopeExecutor.ScheduledOperations)
        {
            await operation();
        }

        // Assert
        outbox.Verify(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenOccurredUtcIsDefault_StampsFromClock()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.Equal(_now, interactionEvent.OccurredUtc);
    }

    [Fact]
    public async Task PublishAsync_WhenItemIdIsEmpty_AssignsAnId()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrEmpty(interactionEvent.ItemId));
    }

    [Fact]
    public async Task PublishAsync_WhenActorIdIsEmpty_SetsTheSystemActor()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.Equal(ContactCenterConstants.SystemActor, interactionEvent.ActorId);
    }

    [Fact]
    public async Task PublishAsync_WhenIdempotencyKeyAlreadyExists_SkipsPersistAndDispatch()
    {
        // Arrange
        var store = CreateStore();
        store
            .Setup(s => s.ExistsByIdempotencyKeyAsync("dup-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var outbox = new Mock<IContactCenterOutbox>();
        var publisher = CreatePublisher(store.Object, outbox.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            IdempotencyKey = "dup-key",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        outbox.Verify(o => o.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenConcurrentDuplicatesBothReadMissing_PersistsBothToday()
    {
        // Arrange
        var existenceGate = new AsyncGate(2);
        var store = CreateStore();
        store
            .Setup(service => service.ExistsByIdempotencyKeyAsync("duplicate-event", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, _) =>
            {
                await existenceGate.SignalAndWaitAsync();

                return false;
            });
        var outbox = new Mock<IContactCenterOutbox>();
        var publisher = CreatePublisher(store.Object, outbox.Object);
        var firstEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = "interaction-1",
            IdempotencyKey = "duplicate-event",
        };
        var secondEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = "interaction-1",
            IdempotencyKey = "duplicate-event",
        };

        // Act
        await Task.WhenAll(
            publisher.PublishAsync(firstEvent, TestContext.Current.CancellationToken),
            publisher.PublishAsync(secondEvent, TestContext.Current.CancellationToken));

        // Assert
        store.Verify(
            service => service.CreateAsync(
                It.Is<InteractionEvent>(interactionEvent => interactionEvent.IdempotencyKey == "duplicate-event"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        outbox.Verify(
            service => service.EnqueueAsync(
                It.Is<InteractionEvent>(interactionEvent => interactionEvent.IdempotencyKey == "duplicate-event"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static Mock<IInteractionEventStore> CreateStore()
    {
        var store = new Mock<IInteractionEventStore>();
        store
            .Setup(s => s.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        store
            .Setup(s => s.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return store;
    }

    private static DefaultContactCenterEventPublisher CreatePublisher(
        IInteractionEventStore store,
        IContactCenterOutbox outbox,
        IContactCenterScopeExecutor scopeExecutor = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DefaultContactCenterEventPublisher(
            store,
            outbox,
            scopeExecutor ?? new TestContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider()),
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
    }

    private sealed class AsyncGate(int participantCount)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == participantCount)
            {
                _completion.TrySetResult();
            }

            return _completion.Task;
        }
    }
}

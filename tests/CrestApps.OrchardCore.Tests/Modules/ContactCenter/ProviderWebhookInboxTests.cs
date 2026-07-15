using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderWebhookInboxTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptAsync_WhenDeliveryIsNew_CommitsPendingMessageBeforeReturning()
    {
        // Arrange
        var store = new Mock<IProviderWebhookInboxStore>();
        ProviderWebhookInboxMessage created = null;
        store
            .Setup(service => service.CreateAsync(
                It.IsAny<ProviderWebhookInboxMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProviderWebhookInboxMessage, CancellationToken>((message, _) => created = message)
            .Returns(ValueTask.CompletedTask);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new Mock<IProviderWebhookInboxHandler>();
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        var result = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Accepted, result.Status);
        Assert.NotNull(created);
        Assert.Equal(result.MessageId, created.ItemId);
        Assert.Equal(ProviderWebhookInboxStatus.Pending, created.Status);
        Assert.Equal(_now, created.NextAttemptUtc);
        session.Verify(
            service => service.SaveChangesAsync(TestContext.Current.CancellationToken),
            Times.Once);
        handler.Verify(
            service => service.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenDeliveryAlreadyExists_ReturnsDuplicateWithoutWriting()
    {
        // Arrange
        var existing = new ProviderWebhookInboxMessage { ItemId = "message-1" };
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByDeliveryAsync(
                "provider",
                "delivery-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var session = new Mock<ISession>();
        var inbox = CreateInbox(store, session, []);

        // Act
        var result = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Duplicate, result.Status);
        Assert.Equal(existing.ItemId, result.MessageId);
        store.Verify(
            service => service.CreateAsync(
                It.IsAny<ProviderWebhookInboxMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        session.Verify(
            service => service.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenDistributedLockIsBusy_DoesNotAcknowledgeDelivery()
    {
        // Arrange
        var store = new Mock<IProviderWebhookInboxStore>();
        var session = new Mock<ISession>();
        var inbox = CreateInbox(store, session, [], locked: false);

        // Act
        var result = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Busy, result.Status);
        Assert.Null(result.MessageId);
        store.Verify(
            service => service.FindByDeliveryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        session.Verify(
            service => service.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenDeliveryIdentifierExceedsIndexLimit_RejectsBeforeLocking()
    {
        // Arrange
        var store = new Mock<IProviderWebhookInboxStore>();
        var session = new Mock<ISession>();
        var inbox = CreateInbox(store, session, []);
        var delivery = CreateDelivery();
        delivery.DeliveryId = new string('x', ProviderWebhookInbox.MaxDeliveryIdLength + 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inbox.AcceptAsync(delivery, TestContext.Current.CancellationToken));
        store.Verify(
            service => service.FindByDeliveryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerSucceeds_DeletesAndCommitsMessage()
    {
        // Arrange
        var message = CreateMessage();
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByIdAsync(message.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .Setup(service => service.HandleAsync(message.Payload, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        var completed = await inbox.DispatchAsync(message.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(completed);
        handler.Verify(
            service => service.HandleAsync(message.Payload, TestContext.Current.CancellationToken),
            Times.Once);
        store.Verify(
            service => service.DeleteAsync(message, TestContext.Current.CancellationToken),
            Times.Once);
        session.Verify(
            service => service.SaveChangesAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerFails_SchedulesPersistedRetry()
    {
        // Arrange
        var message = CreateMessage();
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByIdAsync(message.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .Setup(service => service.HandleAsync(message.Payload, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive provider response"));
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        var completed = await inbox.DispatchAsync(message.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(completed);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(typeof(InvalidOperationException).FullName, message.LastError);
        Assert.Equal(_now.AddSeconds(15), message.NextAttemptUtc);
        store.Verify(
            service => service.UpdateAsync(message, TestContext.Current.CancellationToken),
            Times.Once);
        session.Verify(
            service => service.SaveChangesAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenFeatureScopedHandlerIsUnavailable_DefersWithoutConsumingRetryBudget()
    {
        // Arrange
        var message = CreateMessage();
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByIdAsync(message.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inbox = CreateInbox(store, session, []);

        // Act
        var completed = await inbox.DispatchAsync(message.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(completed);
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal("HandlerUnavailable", message.LastError);
        Assert.Equal(_now.AddMinutes(5), message.NextAttemptUtc);
        Assert.Equal(ProviderWebhookInboxStatus.Pending, message.Status);
        store.Verify(
            service => service.UpdateAsync(message, TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenOneMessageFails_ContinuesToLaterMessages()
    {
        // Arrange
        var first = CreateMessage("message-1");
        var second = CreateMessage("message-2");
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.ListDueAsync(_now, ProviderWebhookInbox.MaxBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        store
            .Setup(service => service.FindByIdAsync(first.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);
        store
            .Setup(service => service.FindByIdAsync(second.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(second);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .SetupSequence(service => service.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("poison"))
            .Returns(Task.CompletedTask);
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        var completed = await inbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, completed);
        Assert.Equal(1, first.AttemptCount);
        store.Verify(
            service => service.DeleteAsync(second, TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenCommitThrowsConcurrency_RethrowsWithoutSchedulingRetry()
    {
        // Arrange
        var message = CreateMessage();
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByIdAsync(message.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .Setup(service => service.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            inbox.DispatchAsync(message.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal(ProviderWebhookInboxStatus.Pending, message.Status);
        store.Verify(
            service => service.UpdateAsync(It.IsAny<ProviderWebhookInboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenAMessageLosesConcurrency_StopsBatchWithoutReusingSession()
    {
        // Arrange
        var first = CreateMessage("message-1");
        var second = CreateMessage("message-2");
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.ListDueAsync(_now, ProviderWebhookInbox.MaxBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        store
            .Setup(service => service.FindByIdAsync(first.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);
        store
            .Setup(service => service.FindByIdAsync(second.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(second);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .Setup(service => service.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        var completed = await inbox.DispatchDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, completed);

        // The batch stops after the first concurrency loss so the canceled session is never reused for the
        // remaining message; it is retried on the next pass in a fresh scope.
        store.Verify(
            service => service.FindByIdAsync(second.ItemId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenRetryBudgetIsExhausted_DeadLettersMessage()
    {
        // Arrange
        var message = CreateMessage();
        message.AttemptCount = ProviderWebhookInbox.MaxAttempts - 1;
        var store = new Mock<IProviderWebhookInboxStore>();
        store
            .Setup(service => service.FindByIdAsync(message.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new Mock<IProviderWebhookInboxHandler>();
        handler.SetupGet(service => service.TechnicalName).Returns("handler");
        handler
            .Setup(service => service.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var inbox = CreateInbox(store, session, [handler.Object]);

        // Act
        await inbox.DispatchAsync(message.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderWebhookInboxStatus.DeadLettered, message.Status);
        Assert.Equal(ProviderWebhookInbox.MaxAttempts, message.AttemptCount);
    }

    private static ProviderWebhookInbox CreateInbox(
        Mock<IProviderWebhookInboxStore> store,
        Mock<ISession> session,
        IEnumerable<IProviderWebhookInboxHandler> handlers,
        bool locked = true)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, locked));
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new ProviderWebhookInbox(
            handlers,
            store.Object,
            session.Object,
            distributedLock.Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderWebhookInbox>.Instance);
    }

    private static ProviderWebhookInboxDelivery CreateDelivery()
    {
        return new ProviderWebhookInboxDelivery
        {
            ProviderName = "provider",
            DeliveryId = "delivery-1",
            HandlerName = "handler",
            Payload = "{}",
        };
    }

    private static ProviderWebhookInboxMessage CreateMessage(string itemId = "message-1")
    {
        return new ProviderWebhookInboxMessage
        {
            ItemId = itemId,
            ProviderName = "provider",
            DeliveryId = $"delivery-{itemId}",
            HandlerName = "handler",
            Payload = "{}",
            Status = ProviderWebhookInboxStatus.Pending,
            NextAttemptUtc = _now,
            CreatedUtc = _now,
        };
    }
}

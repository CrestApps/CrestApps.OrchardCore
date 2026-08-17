using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

public sealed class DefaultTransactionReminderServiceTests
{
    private static readonly DateTime _now = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendReminderAsync_WhenNotificationSucceeds_RecordsReminderAndReturnsTrue()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var service = CreateService(successfulCount: 1, ownerFound: true);

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Equal(1, transaction.ReminderCount);
        Assert.Equal(_now, transaction.LastReminderSentUtc);
        Assert.Contains(transaction.Events, e => e.Type == TransactionEventType.ReminderSent);
    }

    [Fact]
    public async Task SendReminderAsync_WhenNotificationFails_ReturnsFalseWithoutRecording()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var service = CreateService(successfulCount: 0, ownerFound: true);

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        Assert.Equal(0, transaction.ReminderCount);
        Assert.Null(transaction.LastReminderSentUtc);
        Assert.DoesNotContain(transaction.Events, e => e.Type == TransactionEventType.ReminderSent);
    }

    [Fact]
    public async Task SendReminderAsync_WhenOwnerIsMissing_ReturnsFalse()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        var service = CreateService(successfulCount: 1, ownerFound: false);

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        Assert.Equal(0, transaction.ReminderCount);
    }

    [Fact]
    public async Task SendReminderAsync_WhenNothingIsOutstanding_ReturnsFalse()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        transaction.AmountPaid = transaction.TotalAmount;

        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var userService = new Mock<IUserService>(MockBehavior.Strict);
        var service = new DefaultTransactionReminderService(
            notificationService.Object,
            userService.Object,
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        notificationService.Verify(
            s => s.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Transaction CreateOutstandingTransaction()
        => new()
        {
            ItemId = "transaction-1",
            Title = "Outstanding order",
            OwnerId = "owner-1",
            Currency = "USD",
            TotalAmount = 108m,
            AmountPaid = 0m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };

    private static DefaultTransactionReminderService CreateService(int successfulCount, bool ownerFound)
    {
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(s => s.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = successfulCount });

        var userService = new Mock<IUserService>();
        userService
            .Setup(s => s.GetUserByUniqueIdAsync(It.IsAny<string>()))
            .ReturnsAsync(ownerFound ? Mock.Of<IUser>() : null);

        return new DefaultTransactionReminderService(
            notificationService.Object,
            userService.Object,
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }
}

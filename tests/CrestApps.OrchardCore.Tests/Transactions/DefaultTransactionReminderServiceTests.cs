using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
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
        var contactResolver = new Mock<ICustomerContactResolver>(MockBehavior.Strict);

        var service = new DefaultTransactionReminderService(
            notificationService.Object,
            userService.Object,
            contactResolver.Object,
            EmptyServiceProvider(),
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

    [Fact]
    public async Task SendReminderAsync_WhenGuestOwnerWithEmail_DeliversByEmailAndRecords()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        transaction.OwnerKind = CustomerOwnerKind.Guest;
        transaction.OwnerId = "guest-1";
        transaction.GuestContactName = "Guest";
        transaction.GuestContactEmail = "guest@example.com";

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var contactResolver = new Mock<ICustomerContactResolver>();
        contactResolver
            .Setup(s => s.ResolveAsync(It.IsAny<CustomerOwner>(), It.IsAny<ICustomerContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerOwner _, ICustomerContact guest, CancellationToken _) => guest);

        var service = new DefaultTransactionReminderService(
            new Mock<INotificationService>(MockBehavior.Strict).Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            contactResolver.Object,
            ServiceProviderWithEmail(emailService.Object),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Equal(1, transaction.ReminderCount);
        emailService.Verify(
            s => s.SendAsync(It.Is<MailMessage>(m => m.To == "guest@example.com"), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendReminderAsync_WhenGuestOwnerHasNoEmail_ReturnsFalse()
    {
        // Arrange
        var transaction = CreateOutstandingTransaction();
        transaction.OwnerKind = CustomerOwnerKind.Guest;
        transaction.OwnerId = "guest-1";

        var contactResolver = new Mock<ICustomerContactResolver>();
        contactResolver
            .Setup(s => s.ResolveAsync(It.IsAny<CustomerOwner>(), It.IsAny<ICustomerContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerOwner _, ICustomerContact guest, CancellationToken _) => guest);

        var service = new DefaultTransactionReminderService(
            new Mock<INotificationService>(MockBehavior.Strict).Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            contactResolver.Object,
            EmptyServiceProvider(),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());

        // Act
        var result = await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        Assert.Equal(0, transaction.ReminderCount);
    }

    private static Transaction CreateOutstandingTransaction()
        => new()
        {
            ItemId = "transaction-1",
            Title = "Outstanding order",
            OwnerId = "owner-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
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
            new Mock<ICustomerContactResolver>().Object,
            EmptyServiceProvider(),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }

    private static ServiceProvider EmptyServiceProvider()
        => new ServiceCollection().BuildServiceProvider();

    private static ServiceProvider ServiceProviderWithEmail(IEmailService emailService)
        => new ServiceCollection().AddSingleton(emailService).BuildServiceProvider();
}

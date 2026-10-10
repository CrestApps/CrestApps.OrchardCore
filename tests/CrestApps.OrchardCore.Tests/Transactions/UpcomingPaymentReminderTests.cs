using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A customer is told a payment is coming due before it does, once, and a payment that will be charged to a saved
/// card says which card and when, so a charge is never a surprise.
/// </summary>
public sealed class UpcomingPaymentReminderTests
{
    private static readonly DateTime _now = new(2027, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2, null, true)]
    [InlineData(3, null, true)]
    [InlineData(4, null, false)]
    [InlineData(-1, null, false)]
    [InlineData(2, 1, false)]
    public void IsUpcomingReminderDue_SendsOnceInsideTheWindow(int dueInDays, int? sentDaysAgo, bool expected)
    {
        // Arrange
        var transaction = new Transaction
        {
            Status = TransactionStatus.Pending,
            TotalAmount = 50m,
            DueUtc = _now.AddDays(dueInDays),
            UpcomingReminderSentUtc = sentDaysAgo.HasValue ? _now.AddDays(-sentDaysAgo.Value) : null,
        };

        // Act
        var due = TransactionReminderBackgroundTask.IsUpcomingReminderDue(transaction, new TransactionReminderSettings { UpcomingReminderDays = 3 }, _now);

        // Assert
        Assert.Equal(expected, due);
    }

    [Fact]
    public void IsUpcomingReminderDue_WhenTurnedOff_SendsNothing()
        => Assert.False(TransactionReminderBackgroundTask.IsUpcomingReminderDue(
            new Transaction { TotalAmount = 50m, DueUtc = _now.AddDays(1) },
            new TransactionReminderSettings { UpcomingReminderDays = 0 },
            _now));

    [Fact]
    public async Task SendUpcomingReminderAsync_ForAnAutomaticCharge_NamesTheCardAndRecordsIt()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateService(message => sent = message);
        var transaction = new Transaction
        {
            ItemId = "tx-1",
            Title = "Kitchen remodel — payment 2 of 6",
            OwnerId = "customer-1",
            Currency = "USD",
            TotalAmount = 150m,
            Status = TransactionStatus.Pending,
            DueUtc = _now.AddDays(3),
            AutoCollection = new TransactionAutoCollection { ProviderKey = "Stripe", PaymentMethodDescription = "Visa •••• 4242" },
        };

        // Act
        var result = await service.SendUpcomingReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Contains("Visa •••• 4242", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("USD 150.00", sent.TextBody, StringComparison.Ordinal);
        Assert.Equal(_now, transaction.UpcomingReminderSentUtc);
        Assert.Equal(0, transaction.ReminderCount);
        Assert.Contains(transaction.Events, evt => evt.Type == TransactionEventType.ReminderSent);
    }

    [Fact]
    public async Task SendUpcomingReminderAsync_ForAPaymentTheCustomerMakes_AsksThemToPay()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateService(message => sent = message);
        var transaction = new Transaction
        {
            ItemId = "tx-1",
            Title = "Kitchen remodel — payment 2 of 6",
            OwnerId = "customer-1",
            Currency = "USD",
            TotalAmount = 150m,
            Status = TransactionStatus.Pending,
            DueUtc = _now.AddDays(3),
        };

        // Act
        await service.SendUpcomingReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("sign in to pay", sent.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackgroundTask_SendsTheUpcomingReminderOnlyOnce()
    {
        // Arrange
        var store = new FakeTransactionStore(new Transaction
        {
            ItemId = "tx-1",
            OwnerId = "customer-1",
            Currency = "USD",
            TotalAmount = 150m,
            Status = TransactionStatus.Pending,
            DueUtc = _now.AddDays(2),
            CreatedUtc = _now.AddDays(-30),
        });

        var sent = 0;
        var services = new ServiceCollection()
            .AddSingleton(SiteServiceFactory.Create(new TransactionReminderSettings()))
            .AddSingleton<global::OrchardCore.Modules.IClock>(new TestClock(_now))
            .AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store))
            .AddSingleton<ITransactionReminderService>(CreateService(_ => sent++))
            .AddLogging()
            .BuildServiceProvider();

        var task = new TransactionReminderBackgroundTask();

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, sent);
    }

    private static DefaultTransactionReminderService CreateService(Action<INotificationMessage> onSend)
    {
        var user = Mock.Of<IUser>();

        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(user);

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => onSend(message))
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        return new DefaultTransactionReminderService(
            notifications.Object,
            userService.Object,
            Mock.Of<ICustomerContactResolver>(),
            new ServiceCollection().BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }
}

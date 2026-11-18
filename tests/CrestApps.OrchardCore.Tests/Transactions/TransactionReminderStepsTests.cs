using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The staged reminder policy sends one reminder per step relative to the due date (before, on and after), only the
/// latest step that has come, never to a paused transaction or one charged to a saved card while the charge is pending,
/// and only in the site's daytime hours. A declined charge of a saved card is retried on its schedule.
/// </summary>
public sealed class TransactionReminderStepsTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TransactionReminderSettings _staged = new() { Steps = [7, -3, 0, 14, 0] };

    [Theory]
    [InlineData(5, 0, null)]
    [InlineData(3, 0, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(-7, 0, 2)]
    [InlineData(-30, 0, 3)]
    [InlineData(-30, 4, null)]
    [InlineData(-8, 2, 2)]
    [InlineData(-8, 3, null)]
    public void TheDueStep_IsTheLatestStepThatHasCome_AndWasNotSent(int dueInDays, int stepsSent, int? expected)
    {
        // Arrange: the steps are -3, 0, 7 and 14 once sorted and without the repeat.
        var transaction = CreateOutstanding(_now.AddDays(dueInDays));
        transaction.ReminderStepsSent = stepsSent;

        // Act
        var step = TransactionReminderSchedule.GetDueStep(transaction, _staged, _now);

        // Assert
        Assert.Equal(expected, step);
    }

    [Fact]
    public void APausedOrPaidTransaction_HasNoDueStep()
    {
        // Arrange
        var paused = CreateOutstanding(_now.AddDays(-7));
        paused.RemindersPaused = true;

        var paid = CreateOutstanding(_now.AddDays(-7));
        paid.AmountPaid = paid.TotalAmount;
        paid.Status = TransactionStatus.Paid;

        // Act & Assert
        Assert.Null(TransactionReminderSchedule.GetDueStep(paused, _staged, _now));
        Assert.Null(TransactionReminderSchedule.GetDueStep(paid, _staged, _now));
        Assert.Equal([-3, 0, 7, 14], TransactionReminderSchedule.GetSteps(_staged));
        Assert.Equal(3, TransactionReminderSchedule.GetLeadDays(_staged));
    }

    [Theory]
    [InlineData(8, 20, 7, false)]
    [InlineData(8, 20, 8, true)]
    [InlineData(8, 20, 19, true)]
    [InlineData(8, 20, 20, false)]
    [InlineData(0, 24, 3, true)]
    [InlineData(22, 6, 23, true)]
    [InlineData(22, 6, 12, false)]
    public void Reminders_AreSentInTheSendingHours(int from, int until, int hour, bool expected)
    {
        // Arrange
        var settings = new TransactionReminderSettings { SendFromHour = from, SendUntilHour = until };

        // Act & Assert
        Assert.Equal(expected, settings.IsSendingHour(hour));
    }

    [Fact]
    public void ADeclinedCharge_IsRetriedOnTheRetrySchedule_ThenNotAgain()
    {
        // Arrange
        var settings = new TransactionReminderSettings { ChargeRetryDays = [1, 3] };

        // Act & Assert
        Assert.Equal(TimeSpan.FromDays(1), settings.GetChargeRetryDelay(1));
        Assert.Equal(TimeSpan.FromDays(3), settings.GetChargeRetryDelay(2));
        Assert.Null(settings.GetChargeRetryDelay(3));
        Assert.Null(settings.GetChargeRetryDelay(0));
    }

    [Fact]
    public async Task TheStagedSweep_SendsTheDueStepOnce_AndSkipsPausedAndCardChargedTransactions()
    {
        // Arrange: one a week overdue, one paused, one to be charged to a card, and one coming due in two days.
        var overdue = CreateOutstanding(_now.AddDays(-7));

        var paused = CreateOutstanding(_now.AddDays(-7), "transaction-2");
        paused.RemindersPaused = true;

        var charged = CreateOutstanding(_now.AddDays(-1), "transaction-3");
        charged.AutoCharge = new TransactionAutoCharge { SavedPaymentMethodId = "card-1", NextAttemptUtc = _now.AddDays(1) };

        var comingDue = CreateOutstanding(_now.AddDays(2), "transaction-4");

        var store = new FakeTransactionStore(overdue, paused, charged, comingDue);
        var reminded = new List<string>();
        var reminders = new Mock<ITransactionReminderService>();
        reminders.Setup(service => service.SendReminderAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction transaction, CancellationToken _) =>
            {
                reminded.Add("owed:" + transaction.ItemId);

                return true;
            });
        reminders.Setup(service => service.SendUpcomingReminderAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction transaction, CancellationToken _) =>
            {
                reminded.Add("upcoming:" + transaction.ItemId);

                return true;
            });

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(_now));

        var services = new ServiceCollection()
            .AddSingleton(SiteServiceFactory.Create(_staged))
            .AddSingleton<IClock>(new TestClock(_now))
            .AddSingleton(localClock.Object)
            .AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store, new RecordingTransactionEventHandler()))
            .AddSingleton(reminders.Object)
            .AddLogging()
            .BuildServiceProvider();

        var task = new TransactionReminderBackgroundTask();

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["owed:transaction-1", "upcoming:transaction-4"], reminded.Order(StringComparer.Ordinal));
        Assert.Equal(3, (await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).ReminderStepsSent);
        Assert.Equal(1, (await store.FindByIdAsync("transaction-4", TestContext.Current.CancellationToken)).ReminderStepsSent);
    }

    [Fact]
    public async Task OutsideTheSendingHours_NoReminderIsSent()
    {
        // Arrange: 06:00 in the site's time zone.
        var store = new FakeTransactionStore(CreateOutstanding(_now.AddDays(-7)));
        var reminders = new Mock<ITransactionReminderService>(MockBehavior.Strict);
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(_now.Date.AddHours(6)));

        var services = new ServiceCollection()
            .AddSingleton(SiteServiceFactory.Create(_staged))
            .AddSingleton<IClock>(new TestClock(_now))
            .AddSingleton(localClock.Object)
            .AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store, new RecordingTransactionEventHandler()))
            .AddSingleton(reminders.Object)
            .AddLogging()
            .BuildServiceProvider();

        // Act
        await new TransactionReminderBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert: the strict mock was never called.
        Assert.Equal(0, (await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).ReminderStepsSent);
    }

    private static Transaction CreateOutstanding(DateTime dueUtc, string id = "transaction-1")
        => new()
        {
            ItemId = id,
            OwnerId = "customer-1",
            Title = "Fall registration",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            DueUtc = dueUtc,
            CreatedUtc = _now.AddDays(-30),
            UpdatedUtc = _now.AddDays(-30),
        };
}

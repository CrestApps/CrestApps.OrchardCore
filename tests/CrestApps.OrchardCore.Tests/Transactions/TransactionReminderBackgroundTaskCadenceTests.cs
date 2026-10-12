using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Tasks;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

public sealed class TransactionReminderBackgroundTaskCadenceTests
{
    private static readonly DateTime _now = new(2024, 4, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsReminderDue_WhenNothingIsOutstanding_ReturnsFalse()
    {
        var transaction = CreateTransaction(reminderCount: 0, dueUtc: _now.AddDays(-10));
        transaction.AmountPaid = transaction.TotalAmount;

        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(transaction, new TransactionReminderSettings(), _now));
    }

    [Fact]
    public void IsReminderDue_WhenMaxRemindersReached_ReturnsFalse()
    {
        var transaction = CreateTransaction(reminderCount: 3, dueUtc: _now.AddDays(-30), lastReminderUtc: _now.AddDays(-30));
        var settings = new TransactionReminderSettings { MaxReminders = 3 };

        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(transaction, settings, _now));
    }

    [Fact]
    public void IsReminderDue_ForFirstReminderBeforeDelayHasElapsed_ReturnsFalse()
    {
        var transaction = CreateTransaction(reminderCount: 0, dueUtc: _now.AddDays(-1));
        var settings = new TransactionReminderSettings { FirstReminderDelayDays = 3 };

        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(transaction, settings, _now));
    }

    [Fact]
    public void IsReminderDue_ForFirstReminderAfterDelayHasElapsed_ReturnsTrue()
    {
        var transaction = CreateTransaction(reminderCount: 0, dueUtc: _now.AddDays(-5));
        var settings = new TransactionReminderSettings { FirstReminderDelayDays = 3 };

        Assert.True(TransactionReminderBackgroundTask.IsReminderDue(transaction, settings, _now));
    }

    [Fact]
    public void IsReminderDue_WhenIntervalHasNotElapsed_ReturnsFalse()
    {
        var transaction = CreateTransaction(reminderCount: 1, dueUtc: _now.AddDays(-20), lastReminderUtc: _now.AddDays(-2));
        var settings = new TransactionReminderSettings { ReminderIntervalDays = 7 };

        Assert.False(TransactionReminderBackgroundTask.IsReminderDue(transaction, settings, _now));
    }

    [Fact]
    public void IsReminderDue_WhenIntervalHasElapsed_ReturnsTrue()
    {
        var transaction = CreateTransaction(reminderCount: 1, dueUtc: _now.AddDays(-20), lastReminderUtc: _now.AddDays(-8));
        var settings = new TransactionReminderSettings { ReminderIntervalDays = 7 };

        Assert.True(TransactionReminderBackgroundTask.IsReminderDue(transaction, settings, _now));
    }

    private static Transaction CreateTransaction(int reminderCount, DateTime? dueUtc, DateTime? lastReminderUtc = null)
        => new()
        {
            ItemId = "transaction-1",
            TotalAmount = 100m,
            AmountPaid = 0m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-30),
            UpdatedUtc = _now,
            DueUtc = dueUtc,
            ReminderCount = reminderCount,
            LastReminderSentUtc = lastReminderUtc,
        };
}

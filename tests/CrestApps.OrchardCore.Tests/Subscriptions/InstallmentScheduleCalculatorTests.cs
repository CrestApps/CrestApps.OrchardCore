using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// An installment schedule must always add up to the plan total, to the minor unit, and fall due on predictable
/// dates. A schedule a cent short is a cent never collected; one a cent over is a cent charged in error.
/// </summary>
public sealed class InstallmentScheduleCalculatorTests
{
    [Fact]
    public void SplitAmounts_EvenSplit_IsEqualPayments()
    {
        // Act
        var amounts = InstallmentScheduleCalculator.SplitAmounts(1200m, 200m, 10, "USD");

        // Assert
        Assert.Equal(10, amounts.Count);
        Assert.All(amounts, amount => Assert.Equal(100m, amount));
    }

    [Fact]
    public void SplitAmounts_UnevenSplit_PutsTheRoundingOnTheLastPayment()
    {
        // Act
        var amounts = InstallmentScheduleCalculator.SplitAmounts(100m, 0.01m, 3, "USD");

        // Assert
        Assert.Equal([33.33m, 33.33m, 33.33m], amounts);
        Assert.Equal(99.99m, amounts.Sum());

        var uneven = InstallmentScheduleCalculator.SplitAmounts(1000m, 100m, 7, "USD");

        Assert.Equal(128.57m, uneven[0]);
        Assert.Equal(128.58m, uneven[6]);
        Assert.Equal(900m, uneven.Sum());
    }

    [Fact]
    public void SplitAmounts_ZeroDecimalCurrency_NeverProducesFractions()
    {
        // Act
        var amounts = InstallmentScheduleCalculator.SplitAmounts(10000m, 1000m, 7, "JPY");

        // Assert
        Assert.All(amounts, amount => Assert.Equal(decimal.Truncate(amount), amount));
        Assert.Equal(9000m, amounts.Sum());
        Assert.Equal(1285m, amounts[0]);
        Assert.Equal(1290m, amounts[6]);
    }

    [Theory]
    [InlineData(100, 100, 3)]
    [InlineData(100, 150, 3)]
    [InlineData(100, 10, 0)]
    public void SplitAmounts_WhenNothingIsLeftToSchedule_ReturnsNothing(decimal total, decimal downPayment, int count)
        => Assert.Empty(InstallmentScheduleCalculator.SplitAmounts(total, downPayment, count, "USD"));

    [Fact]
    public void GetDueDate_Monthly_CountsFromTheFirstDateAndKeepsTheDayAfterAShortMonth()
    {
        // Arrange
        var first = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var dates = Enumerable.Range(1, 4).Select(number => InstallmentScheduleCalculator.GetDueDate(first, InstallmentFrequency.Monthly, number)).ToArray();

        // Assert
        Assert.Equal(new DateTime(2027, 1, 31), dates[0].Date);
        Assert.Equal(new DateTime(2027, 2, 28), dates[1].Date);
        Assert.Equal(new DateTime(2027, 3, 31), dates[2].Date);
        Assert.Equal(new DateTime(2027, 4, 30), dates[3].Date);
    }

    [Theory]
    [InlineData(InstallmentFrequency.Weekly, 3, 14)]
    [InlineData(InstallmentFrequency.BiWeekly, 3, 28)]
    public void GetDueDate_WeeklyFrequencies_StepByDays(InstallmentFrequency frequency, int number, int expectedDays)
    {
        // Arrange
        var first = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var due = InstallmentScheduleCalculator.GetDueDate(first, frequency, number);

        // Assert
        Assert.Equal(expectedDays, (due - first).Days);
    }

    [Fact]
    public void GetDueDate_Quarterly_StepsThreeMonths()
        => Assert.Equal(
            new DateTime(2027, 7, 15),
            InstallmentScheduleCalculator.GetDueDate(new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc), InstallmentFrequency.Quarterly, 3).Date);

    [Fact]
    public void BuildSchedule_NumbersThePaymentsFromOneAndAddsUpToTheBalance()
    {
        // Act
        var schedule = InstallmentScheduleCalculator.BuildSchedule(500m, 50m, 4, InstallmentFrequency.Monthly, new DateTime(2027, 5, 10, 0, 0, 0, DateTimeKind.Utc), "EUR");

        // Assert
        Assert.Equal([1, 2, 3, 4], schedule.Select(payment => payment.Number));
        Assert.Equal(450m, schedule.Sum(payment => payment.Amount));
        Assert.All(schedule, payment => Assert.Equal(InstallmentPaymentStatus.Scheduled, payment.Status));
        Assert.Equal(new DateTime(2027, 8, 10), schedule[3].DueUtc.Date);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    public void GetRetryDelay_FollowsTheConfiguredDays(int attemptsSoFar, int expectedDays)
        => Assert.Equal(TimeSpan.FromDays(expectedDays), new InstallmentPlanSettings().GetRetryDelay(attemptsSoFar));

    [Fact]
    public void GetRetryDelay_AfterTheLastRetry_IsNone()
    {
        Assert.Null(new InstallmentPlanSettings().GetRetryDelay(4));
        Assert.Null(new InstallmentPlanSettings { RetryDays = [] }.GetRetryDelay(1));
    }

    [Theory]
    [InlineData("1, 3, 5", new[] { 1, 3, 5 })]
    [InlineData("2;7", new[] { 2, 7 })]
    [InlineData("", new int[0])]
    public void TryParseRetryDays_AcceptsShortListsOfDays(string value, int[] expected)
    {
        Assert.True(InstallmentPlanSettingsDisplayDriver.TryParseRetryDays(value, out var days));
        Assert.Equal(expected, days);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("1, x")]
    [InlineData("-1")]
    [InlineData("1,1,1,1,1,1,1,1,1,1,1")]
    public void TryParseRetryDays_RejectsAnythingElse(string value)
        => Assert.False(InstallmentPlanSettingsDisplayDriver.TryParseRetryDays(value, out _));

    [Fact]
    public void SavedPaymentMethod_DescribesAndExpires()
    {
        // Arrange
        var card = new SavedPaymentMethod { Brand = "mastercard", Last4 = "4444", ExpirationMonth = 2, ExpirationYear = 2027 };

        // Assert
        Assert.Equal("Mastercard •••• 4444", card.Describe());
        Assert.False(card.IsExpired(new DateTime(2027, 2, 28, 23, 0, 0, DateTimeKind.Utc)));
        Assert.True(card.IsExpired(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Card", new SavedPaymentMethod().Describe());
    }
}

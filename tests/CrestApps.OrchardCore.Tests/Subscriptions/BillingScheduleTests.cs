using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class BillingScheduleTests
{
    private static readonly DateTime _from = new(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void GetNextBillingDate_Day_AddsDays()
    {
        var result = BillingSchedule.GetNextBillingDate(_from, DurationType.Day, 10);

        Assert.Equal(_from.AddDays(10), result);
    }

    [Fact]
    public void GetNextBillingDate_Week_AddsSevenDaysPerWeek()
    {
        var result = BillingSchedule.GetNextBillingDate(_from, DurationType.Week, 3);

        Assert.Equal(_from.AddDays(21), result);
    }

    [Fact]
    public void GetNextBillingDate_Month_AddsMonths()
    {
        var result = BillingSchedule.GetNextBillingDate(_from, DurationType.Month, 2);

        Assert.Equal(_from.AddMonths(2), result);
    }

    [Fact]
    public void GetNextBillingDate_Year_AddsYears()
    {
        var result = BillingSchedule.GetNextBillingDate(_from, DurationType.Year, 1);

        Assert.Equal(_from.AddYears(1), result);
    }

    [Fact]
    public void GetNextBillingDate_PreservesKind()
    {
        var result = BillingSchedule.GetNextBillingDate(_from, DurationType.Month, 1);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetNextBillingDate_NonPositiveDuration_Throws(int duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BillingSchedule.GetNextBillingDate(_from, DurationType.Month, duration));
    }

    [Fact]
    public void GetNextBillingDate_UnsupportedDurationType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BillingSchedule.GetNextBillingDate(_from, (DurationType)999, 1));
    }
}

using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A billing cycle moves a date forward by its unit, the same for a subscription and a recurring transaction, and
/// never by nothing.
/// </summary>
public sealed class DurationTypeExtensionsTests
{
    private static readonly DateTime _start = new(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(DurationType.Day, 3, "2026-02-03")]
    [InlineData(DurationType.Week, 2, "2026-02-14")]
    [InlineData(DurationType.Month, 1, "2026-02-28")]
    [InlineData(DurationType.Year, 1, "2027-01-31")]
    [InlineData(DurationType.Month, 0, "2026-02-28")]
    public void ACycle_AdvancesTheDateByItsUnit_AndAtLeastOnce(DurationType durationType, int count, string expected)
    {
        var expectedDate = DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture).Date;

        Assert.Equal(expectedDate, durationType.AddTo(_start, count).Date);
        Assert.Equal(expectedDate, new Subscription { DurationType = durationType, BillingDuration = count }.Advance(_start).Date);
        Assert.Equal(expectedDate, new TransactionRecurrence { DurationType = durationType, BillingDuration = count }.Advance(_start).Date);
    }
}

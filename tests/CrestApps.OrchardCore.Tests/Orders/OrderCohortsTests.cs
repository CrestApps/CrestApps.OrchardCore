using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Reports;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A cohort is the buyers whose first order was in a month; each later month counts those who bought again in it.
/// </summary>
public sealed class OrderCohortsTests
{
    [Fact]
    public void BuyersAreGroupedByTheirFirstMonth_AndCountedInTheMonthsTheyCameBack()
    {
        var now = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(now);

        var report = new OrderCohortsReport(new OrderReportReader(Mock.Of<IOrderStore>()), Mock.Of<ISiteService>(), clock.Object, new PassThroughStringLocalizer<OrderCohortsReport>());

        Order Paid(string customer, int year, int month, int day)
            => new() { CustomerId = customer, Status = OrderStatus.Paid, PaidUtc = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc) };

        var history = new[]
        {
            // Sam first bought in June and came back in July and September.
            Paid("sam", 2026, 6, 3), Paid("sam", 2026, 7, 9), Paid("sam", 2026, 7, 20), Paid("sam", 2026, 9, 1),

            // Kim first bought in June and never came back.
            Paid("kim", 2026, 6, 15),

            // Lee first bought in May, before the period, so isn't in a cohort.
            Paid("lee", 2026, 5, 2), Paid("lee", 2026, 6, 2),
        };

        var cohorts = report.Build(history, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc);

        var june = Assert.Single(cohorts);
        Assert.Equal(new DateTime(2026, 6, 1), june.Month);
        Assert.Equal(2, june.Buyers);
        Assert.Equal(1, june.ReturnedAny);

        // July (1), August (2) and September (3) have ended; October (4) hasn't, so it's left out.
        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = 0, [3] = 1 }, june.Returned);
    }
}

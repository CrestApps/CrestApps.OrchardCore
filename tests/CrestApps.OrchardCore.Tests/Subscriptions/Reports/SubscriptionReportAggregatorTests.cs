using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Reports;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Reports;

public sealed class SubscriptionReportAggregatorTests
{
    private static readonly DateTime _nowUtc = new(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetSucceededTransactions_FiltersByStatusAndRange()
    {
        var transactions = new[]
        {
            Transaction(100, 10, PaymentStatus.Succeeded, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            Transaction(200, 20, PaymentStatus.Failed, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)),
            Transaction(300, 30, PaymentStatus.Succeeded, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            Transaction(400, 40, PaymentStatus.Succeeded, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
        };

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var result = SubscriptionReportAggregator.GetSucceededTransactions(transactions, from, to);

        Assert.Equal(2, result.Count);
        Assert.All(result, transaction => Assert.Equal(PaymentStatus.Succeeded, transaction.Status));
        Assert.DoesNotContain(result, transaction => transaction.Amount == 300);
    }

    [Fact]
    public void SummarizeRevenue_ComputesTotalsTaxAndAverage()
    {
        var transactions = new[]
        {
            Transaction(100, 10, PaymentStatus.Succeeded, _nowUtc),
            Transaction(300, 30, PaymentStatus.Succeeded, _nowUtc),
            Transaction(200, 20, PaymentStatus.Succeeded, _nowUtc),
        };

        var summary = SubscriptionReportAggregator.SummarizeRevenue(transactions);

        Assert.Equal(600, summary.TotalRevenue);
        Assert.Equal(3, summary.TransactionCount);
        Assert.Equal(60, summary.TotalTax);
        Assert.Equal(200, summary.AverageTransactionValue);
    }

    [Fact]
    public void SummarizeRevenue_WithNoTransactions_ReturnsZeroAverage()
    {
        var summary = SubscriptionReportAggregator.SummarizeRevenue([]);

        Assert.Equal(0, summary.TransactionCount);
        Assert.Equal(0, summary.TotalRevenue);
        Assert.Equal(0, summary.AverageTransactionValue);
    }

    [Fact]
    public void BucketRevenueByMonth_GroupsChronologicallyWithTotals()
    {
        var transactions = new[]
        {
            Transaction(100, 10, PaymentStatus.Succeeded, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            Transaction(50, 5, PaymentStatus.Succeeded, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            Transaction(200, 20, PaymentStatus.Succeeded, new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc)),
        };

        var buckets = SubscriptionReportAggregator.BucketRevenueByMonth(transactions);

        Assert.Equal(2, buckets.Count);

        var january = buckets[0];
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), january.MonthStart);
        Assert.Equal(150, january.Revenue);
        Assert.Equal(15, january.Tax);
        Assert.Equal(2, january.TransactionCount);

        var february = buckets[1];
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), february.MonthStart);
        Assert.Equal(200, february.Revenue);
        Assert.Equal(1, february.TransactionCount);
    }

    [Fact]
    public void GroupByProduct_GroupsByContentTypeOrderedByRevenue()
    {
        var transactions = new[]
        {
            Transaction(100, 10, PaymentStatus.Succeeded, _nowUtc, "Basic"),
            Transaction(50, 5, PaymentStatus.Succeeded, _nowUtc, "Basic"),
            Transaction(500, 50, PaymentStatus.Succeeded, _nowUtc, "Premium"),
        };

        var products = SubscriptionReportAggregator.GroupByProduct(transactions);

        Assert.Equal(2, products.Count);

        var top = products[0];
        Assert.Equal("Premium", top.ContentType);
        Assert.Equal(500, top.GrossRevenue);
        Assert.Equal(1, top.TransactionCount);

        var second = products[1];
        Assert.Equal("Basic", second.ContentType);
        Assert.Equal(150, second.GrossRevenue);
        Assert.Equal(15, second.Tax);
        Assert.Equal(2, second.TransactionCount);
    }

    [Fact]
    public void SummarizeDashboard_ComputesActiveNewExpiringAndSubscribers()
    {
        var subscriptions = new[]
        {
            // Active, started in period, owner-1.
            Subscription("owner-1", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), null),
            // Active and expiring within 30 days, owner-2.
            Subscription("owner-2", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(10)),
            // Expired, owner-1 (duplicate owner).
            Subscription("owner-1", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(-5)),
            // Active but expiring beyond 30 days, owner-3, started outside period.
            Subscription("owner-3", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(90)),
        };

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var summary = SubscriptionReportAggregator.SummarizeDashboard(subscriptions, _nowUtc, from, to);

        Assert.Equal(3, summary.ActiveSubscriptions);
        Assert.Equal(1, summary.NewSubscriptions);
        Assert.Equal(1, summary.ExpiringSubscriptions);
        Assert.Equal(3, summary.TotalSubscribers);
    }

    [Fact]
    public void GetExpiringSubscriptions_ReturnsWithinHorizonOrderedByExpiry()
    {
        var subscriptions = new[]
        {
            Subscription("owner-1", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(20)),
            Subscription("owner-2", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(5)),
            Subscription("owner-3", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(90)),
            Subscription("owner-4", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(-1)),
            Subscription("owner-5", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), null),
        };

        var expiring = SubscriptionReportAggregator.GetExpiringSubscriptions(subscriptions, _nowUtc);

        Assert.Equal(2, expiring.Count);
        Assert.Equal("owner-2", expiring[0].OwnerId);
        Assert.Equal("owner-1", expiring[1].OwnerId);
        Assert.Equal(5, expiring[0].DaysRemaining);
    }

    [Fact]
    public void BucketNewSubscriptionsByMonth_CountsWithinPeriodByMonth()
    {
        var subscriptions = new[]
        {
            Subscription("owner-1", new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), null),
            Subscription("owner-2", new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc), null),
            Subscription("owner-3", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), null),
            // Outside period, should be excluded.
            Subscription("owner-4", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), null),
        };

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var buckets = SubscriptionReportAggregator.BucketNewSubscriptionsByMonth(subscriptions, from, to);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), buckets[0].MonthStart);
        Assert.Equal(2, buckets[0].Count);
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), buckets[1].MonthStart);
        Assert.Equal(1, buckets[1].Count);
    }

    private static SubscriptionTransactionIndex Transaction(
        decimal amount,
        decimal taxAmount,
        PaymentStatus status,
        DateTime createdUtc,
        string contentType = "Subscription")
    {
        return new SubscriptionTransactionIndex
        {
            Amount = amount,
            TaxAmount = taxAmount,
            Status = status,
            CreatedUtc = createdUtc,
            ContentType = contentType,
        };
    }

    private static SubscriptionIndex Subscription(string ownerId, DateTime startedAt, DateTime? expiresAt)
    {
        return new SubscriptionIndex
        {
            OwnerId = ownerId,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            ContentType = "Subscription",
        };
    }
}

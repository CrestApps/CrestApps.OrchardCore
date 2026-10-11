using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Reports;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Reports;

/// <summary>
/// Pins the arithmetic behind the subscription reports.
/// </summary>
/// <remarks>
/// Every case here exists because getting it wrong produces a report that looks right. An overstated revenue
/// figure is filed on a tax return; an overstated subscriber count is reported to a board.
/// </remarks>
public sealed class SubscriptionReportAggregatorTests
{
    private static readonly DateTime _nowUtc = new(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Money that was asked for but never taken must not be reported as revenue.
    /// </summary>
    [Fact]
    public void GetSucceededPayments_ExcludesUnconfirmedAttemptsAndPaymentsOutsideTheRange()
    {
        var attempts = new[]
        {
            Payment(100, 10, PaymentAttemptState.Succeeded, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            Payment(200, 20, PaymentAttemptState.Failed, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)),
            Payment(250, 25, PaymentAttemptState.Created, new DateTime(2026, 1, 13, 0, 0, 0, DateTimeKind.Utc)),
            Payment(300, 30, PaymentAttemptState.Succeeded, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            Payment(400, 40, PaymentAttemptState.Succeeded, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
        };

        var result = SubscriptionReportAggregator.GetSucceededPayments(
            attempts,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, result.Count);
        Assert.All(result, attempt => Assert.Equal(PaymentAttemptState.Succeeded, attempt.State));
        Assert.DoesNotContain(result, attempt => attempt.ConfirmedAmount == 300);
    }

    /// <summary>
    /// Revenue is the amount the provider confirmed, never the amount the checkout expected.
    /// </summary>
    [Fact]
    public void SummarizeRevenue_SumsConfirmedAmountsAndTax()
    {
        var attempts = new[]
        {
            Payment(100, 10, PaymentAttemptState.Succeeded, _nowUtc),
            Payment(300, 30, PaymentAttemptState.Succeeded, _nowUtc),
            Payment(200, 20, PaymentAttemptState.Succeeded, _nowUtc),
        };

        var summary = SubscriptionReportAggregator.SummarizeRevenue(attempts);

        Assert.Equal(600, summary.TotalRevenue);
        Assert.Equal(3, summary.TransactionCount);
        Assert.Equal(60, summary.TotalTax);
        Assert.Equal(200, summary.AverageTransactionValue);
    }

    /// <summary>
    /// A site with no sales must render a report rather than divide by zero.
    /// </summary>
    [Fact]
    public void SummarizeRevenue_WithNoPayments_ReturnsZeroAverage()
    {
        var summary = SubscriptionReportAggregator.SummarizeRevenue([]);

        Assert.Equal(0, summary.TransactionCount);
        Assert.Equal(0, summary.TotalRevenue);
        Assert.Equal(0, summary.AverageTransactionValue);
    }

    /// <summary>
    /// Adding amounts taken in different currencies produces a total that means nothing, so they are kept apart.
    /// </summary>
    [Fact]
    public void GroupByCurrency_KeepsCurrenciesSeparateAndOrdersByRevenue()
    {
        var attempts = new[]
        {
            Payment(100, 0, PaymentAttemptState.Succeeded, _nowUtc, currency: "EUR"),
            Payment(500, 0, PaymentAttemptState.Succeeded, _nowUtc, currency: "USD"),
            Payment(300, 0, PaymentAttemptState.Succeeded, _nowUtc, currency: "usd"),
        };

        var groups = SubscriptionReportAggregator.GroupByCurrency(attempts);

        Assert.Equal(2, groups.Count);
        Assert.Equal("USD", groups[0].Currency);
        Assert.Equal(2, groups[0].Payments.Count);
        Assert.Equal("EUR", groups[1].Currency);
        Assert.Single(groups[1].Payments);
    }

    /// <summary>
    /// Revenue is bucketed by the month the money was taken, in order.
    /// </summary>
    [Fact]
    public void BucketRevenueByMonth_GroupsChronologicallyWithTotals()
    {
        var attempts = new[]
        {
            Payment(100, 10, PaymentAttemptState.Succeeded, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            Payment(50, 5, PaymentAttemptState.Succeeded, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            Payment(200, 20, PaymentAttemptState.Succeeded, new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc)),
        };

        var buckets = SubscriptionReportAggregator.BucketRevenueByMonth(attempts);

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

    /// <summary>
    /// A plan's revenue is grouped by the plan that was bought, and shown by its title rather than its id.
    /// </summary>
    [Fact]
    public void GroupByProduct_GroupsByPlanOrderedByRevenue()
    {
        var attempts = new[]
        {
            Payment(100, 10, PaymentAttemptState.Succeeded, _nowUtc, referenceId: "plan-basic"),
            Payment(50, 5, PaymentAttemptState.Succeeded, _nowUtc, referenceId: "plan-basic"),
            Payment(500, 50, PaymentAttemptState.Succeeded, _nowUtc, referenceId: "plan-premium"),
        };

        var titles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plan-basic"] = "Basic",
            ["plan-premium"] = "Premium",
        };

        var products = SubscriptionReportAggregator.GroupByProduct(attempts, titles);

        Assert.Equal(2, products.Count);

        var top = products[0];
        Assert.Equal("Premium", top.Title);
        Assert.Equal(500, top.GrossRevenue);
        Assert.Equal(1, top.TransactionCount);

        var second = products[1];
        Assert.Equal("Basic", second.Title);
        Assert.Equal(150, second.GrossRevenue);
        Assert.Equal(15, second.Tax);
        Assert.Equal(2, second.TransactionCount);
    }

    /// <summary>
    /// A plan that has since been deleted still has to appear, identified by what is left of it.
    /// </summary>
    [Fact]
    public void GroupByProduct_WithNoTitle_FallsBackToTheIdentifier()
    {
        var products = SubscriptionReportAggregator.GroupByProduct(
        [
            Payment(100, 0, PaymentAttemptState.Succeeded, _nowUtc, referenceId: "plan-gone"),
        ]);

        Assert.Equal("plan-gone", Assert.Single(products).Title);
    }

    /// <summary>
    /// The subscriber base counts agreements by their status, not by whether a date has passed. A canceled
    /// agreement can still be inside its paid period, and counting it as active reports people who have left.
    /// </summary>
    [Fact]
    public void SummarizeDashboard_CountsByStatusAndDoesNotCountLeaversAsActive()
    {
        var subscriptions = new[]
        {
            Agreement("owner-1", SubscriptionStatus.Active, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(300)),
            Agreement("owner-2", SubscriptionStatus.Active, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(10)),
            Agreement("owner-1", SubscriptionStatus.Canceled, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(20)),
            Agreement("owner-3", SubscriptionStatus.PastDue, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc.AddDays(90)),
        };

        var summary = SubscriptionReportAggregator.SummarizeDashboard(
            subscriptions,
            _nowUtc,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));

        // The canceled agreement is excluded even though its paid period has not ended.
        Assert.Equal(3, summary.ActiveSubscriptions);
        Assert.Equal(1, summary.NewSubscriptions);
        Assert.Equal(1, summary.ExpiringSubscriptions);
        Assert.Equal(1, summary.PastDueSubscriptions);

        // owner-1 holds two agreements but is one subscriber.
        Assert.Equal(3, summary.TotalSubscribers);
    }

    /// <summary>
    /// An agreement with no owner must not be counted as an anonymous subscriber.
    /// </summary>
    [Fact]
    public void SummarizeDashboard_IgnoresAgreementsWithNoOwnerWhenCountingSubscribers()
    {
        var summary = SubscriptionReportAggregator.SummarizeDashboard(
        [
            Agreement(null, SubscriptionStatus.Active, _nowUtc, _nowUtc.AddDays(300)),
            Agreement("owner-1", SubscriptionStatus.Active, _nowUtc, _nowUtc.AddDays(300)),
        ], _nowUtc, null, null);

        Assert.Equal(1, summary.TotalSubscribers);
    }

    /// <summary>
    /// The expiring list looks only at agreements that are still current, soonest first.
    /// </summary>
    [Fact]
    public void GetExpiringSubscriptions_ReturnsCurrentAgreementsWithinHorizonOrderedByExpiry()
    {
        var subscriptions = new[]
        {
            Agreement("owner-1", SubscriptionStatus.Active, _nowUtc.AddDays(-400), _nowUtc.AddDays(20)),
            Agreement("owner-2", SubscriptionStatus.Active, _nowUtc.AddDays(-400), _nowUtc.AddDays(5)),
            Agreement("owner-3", SubscriptionStatus.Active, _nowUtc.AddDays(-400), _nowUtc.AddDays(90)),
            Agreement("owner-4", SubscriptionStatus.Active, _nowUtc.AddDays(-400), _nowUtc.AddDays(-1)),
            Agreement("owner-5", SubscriptionStatus.Canceled, _nowUtc.AddDays(-400), _nowUtc.AddDays(3)),
        };

        var expiring = SubscriptionReportAggregator.GetExpiringSubscriptions(subscriptions, _nowUtc);

        Assert.Equal(2, expiring.Count);
        Assert.Equal("owner-2", expiring[0].OwnerId);
        Assert.Equal("owner-1", expiring[1].OwnerId);
        Assert.Equal(5, expiring[0].DaysRemaining);
    }

    /// <summary>
    /// New agreements are bucketed by the month they were created, within the requested period.
    /// </summary>
    [Fact]
    public void BucketNewSubscriptionsByMonth_CountsWithinPeriodByMonth()
    {
        var subscriptions = new[]
        {
            Agreement("owner-1", SubscriptionStatus.Active, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), _nowUtc),
            Agreement("owner-2", SubscriptionStatus.Active, new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc), _nowUtc),
            Agreement("owner-3", SubscriptionStatus.Active, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), _nowUtc),
            Agreement("owner-4", SubscriptionStatus.Active, new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), _nowUtc),
        };

        var buckets = SubscriptionReportAggregator.BucketNewSubscriptionsByMonth(
            subscriptions,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), buckets[0].MonthStart);
        Assert.Equal(2, buckets[0].Count);
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), buckets[1].MonthStart);
        Assert.Equal(1, buckets[1].Count);
    }

    private static PaymentAttemptIndex Payment(
        decimal amount,
        decimal taxAmount,
        PaymentAttemptState state,
        DateTime createdUtc,
        string referenceId = "plan-1",
        string currency = "USD")
        => new()
        {
            ConfirmedAmount = amount,
            ConfirmedTaxAmount = taxAmount,
            State = state,
            CreatedUtc = createdUtc,
            Currency = currency,
            ReferenceId = referenceId,
        };

    private static SubscriptionRecordIndex Agreement(
        string ownerId,
        SubscriptionStatus status,
        DateTime createdUtc,
        DateTime currentPeriodEndUtc)
        => new()
        {
            OwnerId = ownerId,
            Status = status,
            CreatedUtc = createdUtc,
            CurrentPeriodEndUtc = currentPeriodEndUtc,
            Title = "Plan",
        };
}

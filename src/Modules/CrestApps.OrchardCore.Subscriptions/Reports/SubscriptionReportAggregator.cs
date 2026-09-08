using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// The arithmetic behind the subscription reports.
/// </summary>
/// <remarks>
/// Every money figure is computed from the durable payment ledger and every subscriber figure from the
/// durable agreement, never from a checkout session. A session records what somebody was asked to pay; the
/// ledger records what a provider confirmed was taken. Reporting the first as revenue overstates income by
/// every abandoned and failed checkout, which is the difference between a report an owner can file a return
/// from and one they cannot.
/// </remarks>
public static class SubscriptionReportAggregator
{
    /// <summary>
    /// Selects the confirmed payments taken inside a date range.
    /// </summary>
    /// <param name="attempts">The payment attempts to filter.</param>
    /// <param name="fromUtc">The inclusive start of the range, or <see langword="null"/> for no lower bound.</param>
    /// <param name="toUtc">The inclusive end of the range, or <see langword="null"/> for no upper bound.</param>
    /// <returns>The attempts a provider confirmed within the range.</returns>
    public static List<PaymentAttemptIndex> GetSucceededPayments(
        IEnumerable<PaymentAttemptIndex> attempts,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        var from = fromUtc ?? DateTime.MinValue;
        var to = toUtc ?? DateTime.MaxValue;

        return
        [
            .. attempts.Where(attempt => attempt.State == PaymentAttemptState.Succeeded &&
                attempt.CreatedUtc >= from &&
                attempt.CreatedUtc <= to),
        ];
    }

    /// <summary>
    /// Summarizes revenue for one currency.
    /// </summary>
    /// <param name="attempts">The confirmed payments, which must already be filtered to a single currency.</param>
    /// <returns>The revenue summary.</returns>
    public static RevenueSummary SummarizeRevenue(IEnumerable<PaymentAttemptIndex> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        var count = 0;
        var totalRevenue = 0m;
        var totalTax = 0m;

        foreach (var attempt in attempts)
        {
            count++;
            totalRevenue += attempt.ConfirmedAmount;
            totalTax += attempt.ConfirmedTaxAmount;
        }

        return new RevenueSummary
        {
            TotalRevenue = totalRevenue,
            TransactionCount = count,
            TotalTax = totalTax,
            AverageTransactionValue = count == 0 ? 0m : totalRevenue / count,
        };
    }

    /// <summary>
    /// Groups confirmed payments into calendar months.
    /// </summary>
    /// <param name="attempts">The confirmed payments.</param>
    /// <returns>One bucket per month that has revenue, oldest first.</returns>
    public static List<MonthlyRevenueBucket> BucketRevenueByMonth(IEnumerable<PaymentAttemptIndex> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        return
        [
            .. attempts
                .GroupBy(attempt => StartOfMonth(attempt.CreatedUtc))
                .Select(group => new MonthlyRevenueBucket
                {
                    MonthStart = group.Key,
                    Revenue = group.Sum(attempt => attempt.ConfirmedAmount),
                    Tax = group.Sum(attempt => attempt.ConfirmedTaxAmount),
                    TransactionCount = group.Count(),
                })
                .OrderBy(bucket => bucket.MonthStart),
        ];
    }

    /// <summary>
    /// Splits confirmed payments by the currency they were taken in.
    /// </summary>
    /// <param name="attempts">The confirmed payments.</param>
    /// <returns>One group per currency, largest first.</returns>
    /// <remarks>
    /// Amounts in different currencies are never added together. A single "total revenue" across mixed
    /// currencies is a number that means nothing, and silently summing them hides that the site took money
    /// in a currency the owner did not expect.
    /// </remarks>
    public static List<CurrencyRevenueGroup> GroupByCurrency(IEnumerable<PaymentAttemptIndex> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        return
        [
            .. attempts
                .GroupBy(attempt => string.IsNullOrEmpty(attempt.Currency) ? string.Empty : attempt.Currency.ToUpperInvariant(), StringComparer.Ordinal)
                .Select(group => new CurrencyRevenueGroup
                {
                    Currency = group.Key,
                    Payments = [.. group],
                })
                .OrderByDescending(group => group.Payments.Sum(attempt => attempt.ConfirmedAmount))
                .ThenBy(group => group.Currency, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Groups confirmed payments by the plan that was bought.
    /// </summary>
    /// <param name="attempts">The confirmed payments, already filtered to a single currency.</param>
    /// <param name="titles">The display title of each plan, keyed by its reference id.</param>
    /// <returns>One row per plan, highest revenue first.</returns>
    public static List<ProductPerformanceRow> GroupByProduct(
        IEnumerable<PaymentAttemptIndex> attempts,
        IReadOnlyDictionary<string, string> titles = null)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        return
        [
            .. attempts
                .GroupBy(attempt => attempt.ReferenceId ?? string.Empty, StringComparer.Ordinal)
                .Select(group => new ProductPerformanceRow
                {
                    ReferenceId = group.Key,
                    Title = titles is not null && titles.TryGetValue(group.Key, out var title) ? title : group.Key,
                    TransactionCount = group.Count(),
                    GrossRevenue = group.Sum(attempt => attempt.ConfirmedAmount),
                    Tax = group.Sum(attempt => attempt.ConfirmedTaxAmount),
                })
                .OrderByDescending(row => row.GrossRevenue)
                .ThenBy(row => row.Title, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Summarizes the subscriber base.
    /// </summary>
    /// <param name="subscriptions">The durable subscription agreements.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="periodFromUtc">The inclusive start of the "new subscriptions" period.</param>
    /// <param name="periodToUtc">The inclusive end of the "new subscriptions" period.</param>
    /// <param name="expiringHorizonDays">How far ahead an agreement counts as expiring.</param>
    /// <returns>The dashboard summary.</returns>
    public static DashboardSummary SummarizeDashboard(
        IEnumerable<SubscriptionRecordIndex> subscriptions,
        DateTime nowUtc,
        DateTime? periodFromUtc,
        DateTime? periodToUtc,
        int expiringHorizonDays = 30)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var from = periodFromUtc ?? DateTime.MinValue;
        var to = periodToUtc ?? DateTime.MaxValue;
        var horizon = nowUtc.AddDays(expiringHorizonDays);

        var active = 0;
        var newInPeriod = 0;
        var expiring = 0;
        var pastDue = 0;
        var subscribers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subscription in subscriptions)
        {
            // "Active" is the status the agreement carries, not a date comparison. A canceled agreement can
            // still be inside its paid period, and counting it as active would overstate the subscriber base
            // with people who have already left.
            if (IsCurrent(subscription.Status))
            {
                active++;
                subscribers.Add(subscription.OwnerId ?? string.Empty);
            }

            if (subscription.Status == SubscriptionStatus.PastDue)
            {
                pastDue++;
            }

            if (subscription.CreatedUtc >= from && subscription.CreatedUtc <= to)
            {
                newInPeriod++;
            }

            if (IsCurrent(subscription.Status) &&
                subscription.CurrentPeriodEndUtc > nowUtc &&
                subscription.CurrentPeriodEndUtc <= horizon)
            {
                expiring++;
            }
        }

        subscribers.Remove(string.Empty);

        return new DashboardSummary
        {
            ActiveSubscriptions = active,
            NewSubscriptions = newInPeriod,
            ExpiringSubscriptions = expiring,
            PastDueSubscriptions = pastDue,
            TotalSubscribers = subscribers.Count,
        };
    }

    /// <summary>
    /// Lists the agreements whose paid period ends soon.
    /// </summary>
    /// <param name="subscriptions">The durable subscription agreements.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="horizonDays">How far ahead to look.</param>
    /// <returns>The expiring agreements, soonest first.</returns>
    public static List<ExpiringSubscriptionRow> GetExpiringSubscriptions(
        IEnumerable<SubscriptionRecordIndex> subscriptions,
        DateTime nowUtc,
        int horizonDays = 30)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var horizon = nowUtc.AddDays(horizonDays);

        return
        [
            .. subscriptions
                .Where(subscription => IsCurrent(subscription.Status) &&
                    subscription.CurrentPeriodEndUtc > nowUtc &&
                    subscription.CurrentPeriodEndUtc <= horizon)
                .OrderBy(subscription => subscription.CurrentPeriodEndUtc)
                .Select(subscription => new ExpiringSubscriptionRow
                {
                    OwnerId = subscription.OwnerId,
                    Title = subscription.Title,
                    Status = subscription.Status,
                    StartedAt = subscription.CreatedUtc,
                    ExpiresAt = subscription.CurrentPeriodEndUtc,
                    DaysRemaining = (int)Math.Ceiling((subscription.CurrentPeriodEndUtc - nowUtc).TotalDays),
                }),
        ];
    }

    /// <summary>
    /// Groups agreements into the calendar month they started in.
    /// </summary>
    /// <param name="subscriptions">The durable subscription agreements.</param>
    /// <param name="fromUtc">The inclusive start of the range.</param>
    /// <param name="toUtc">The inclusive end of the range.</param>
    /// <returns>One bucket per month, oldest first.</returns>
    public static List<MonthlySubscriptionBucket> BucketNewSubscriptionsByMonth(
        IEnumerable<SubscriptionRecordIndex> subscriptions,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var from = fromUtc ?? DateTime.MinValue;
        var to = toUtc ?? DateTime.MaxValue;

        return
        [
            .. subscriptions
                .Where(subscription => subscription.CreatedUtc >= from && subscription.CreatedUtc <= to)
                .GroupBy(subscription => StartOfMonth(subscription.CreatedUtc))
                .Select(group => new MonthlySubscriptionBucket
                {
                    MonthStart = group.Key,
                    Count = group.Count(),
                })
                .OrderBy(bucket => bucket.MonthStart),
        ];
    }

    private static bool IsCurrent(SubscriptionStatus status)
        => status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue;

    private static DateTime StartOfMonth(DateTime value)
        => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}

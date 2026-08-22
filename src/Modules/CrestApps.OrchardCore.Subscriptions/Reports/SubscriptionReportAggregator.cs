using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// Provides the pure aggregation logic used by the subscription reports. Every method operates on plain
/// in-memory collections of index records so it can be unit-tested without a database.
/// </summary>
public static class SubscriptionReportAggregator
{
    /// <summary>
    /// Returns the succeeded transactions whose <see cref="SubscriptionTransactionIndex.CreatedUtc"/>
    /// falls within the inclusive <paramref name="fromUtc"/>–<paramref name="toUtc"/> window. A
    /// <see langword="null"/> bound is treated as unbounded.
    /// </summary>
    /// <param name="transactions">The transactions to filter.</param>
    /// <param name="fromUtc">The inclusive lower bound, or <see langword="null"/> for no lower bound.</param>
    /// <param name="toUtc">The inclusive upper bound, or <see langword="null"/> for no upper bound.</param>
    /// <returns>The matching succeeded transactions.</returns>
    public static List<SubscriptionTransactionIndex> GetSucceededTransactions(
        IEnumerable<SubscriptionTransactionIndex> transactions,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var from = fromUtc ?? DateTime.MinValue;
        var to = toUtc ?? DateTime.MaxValue;

        return transactions
            .Where(transaction => transaction.Status == PaymentStatus.Succeeded &&
                transaction.CreatedUtc >= from &&
                transaction.CreatedUtc <= to)
            .ToList();
    }

    /// <summary>
    /// Computes the revenue headline metrics for a set of succeeded transactions.
    /// </summary>
    /// <param name="transactions">The succeeded transactions in the reporting period.</param>
    /// <returns>The revenue summary.</returns>
    public static RevenueSummary SummarizeRevenue(IEnumerable<SubscriptionTransactionIndex> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var count = 0;
        var totalRevenue = 0m;
        var totalTax = 0m;

        foreach (var transaction in transactions)
        {
            count++;
            totalRevenue += transaction.Amount;
            totalTax += transaction.TaxAmount;
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
    /// Buckets succeeded transactions into chronological monthly totals of revenue, tax, and count.
    /// Only months that contain at least one transaction are returned.
    /// </summary>
    /// <param name="transactions">The succeeded transactions in the reporting period.</param>
    /// <returns>The monthly revenue buckets, ordered from oldest to newest.</returns>
    public static List<MonthlyRevenueBucket> BucketRevenueByMonth(IEnumerable<SubscriptionTransactionIndex> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .GroupBy(transaction => StartOfMonth(transaction.CreatedUtc))
            .Select(group => new MonthlyRevenueBucket
            {
                MonthStart = group.Key,
                Revenue = group.Sum(transaction => transaction.Amount),
                Tax = group.Sum(transaction => transaction.TaxAmount),
                TransactionCount = group.Count(),
            })
            .OrderBy(bucket => bucket.MonthStart)
            .ToList();
    }

    /// <summary>
    /// Groups succeeded transactions by content type into product-performance rows, ordered by gross
    /// revenue descending.
    /// </summary>
    /// <param name="transactions">The succeeded transactions in the reporting period.</param>
    /// <returns>The product-performance rows.</returns>
    public static List<ProductPerformanceRow> GroupByProduct(IEnumerable<SubscriptionTransactionIndex> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .GroupBy(transaction => transaction.ContentType ?? string.Empty, StringComparer.Ordinal)
            .Select(group => new ProductPerformanceRow
            {
                ContentType = group.Key,
                TransactionCount = group.Count(),
                GrossRevenue = group.Sum(transaction => transaction.Amount),
                Tax = group.Sum(transaction => transaction.TaxAmount),
            })
            .OrderByDescending(row => row.GrossRevenue)
            .ThenBy(row => row.ContentType, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Computes the subscription dashboard headline metrics.
    /// </summary>
    /// <param name="subscriptions">The subscription index records.</param>
    /// <param name="nowUtc">The current UTC time used to evaluate active and expiring subscriptions.</param>
    /// <param name="periodFromUtc">The inclusive lower bound used to count new subscriptions, or <see langword="null"/>.</param>
    /// <param name="periodToUtc">The inclusive upper bound used to count new subscriptions, or <see langword="null"/>.</param>
    /// <param name="expiringHorizonDays">The look-ahead horizon, in days, used to count expiring subscriptions.</param>
    /// <returns>The dashboard summary.</returns>
    public static DashboardSummary SummarizeDashboard(
        IEnumerable<SubscriptionIndex> subscriptions,
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
        var subscribers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subscription in subscriptions)
        {
            if (subscription.ExpiresAt is null || subscription.ExpiresAt > nowUtc)
            {
                active++;
            }

            if (subscription.StartedAt >= from && subscription.StartedAt <= to)
            {
                newInPeriod++;
            }

            if (subscription.ExpiresAt is not null &&
                subscription.ExpiresAt > nowUtc &&
                subscription.ExpiresAt <= horizon)
            {
                expiring++;
            }

            if (!string.IsNullOrEmpty(subscription.OwnerId))
            {
                subscribers.Add(subscription.OwnerId);
            }
        }

        return new DashboardSummary
        {
            ActiveSubscriptions = active,
            NewSubscriptions = newInPeriod,
            ExpiringSubscriptions = expiring,
            TotalSubscribers = subscribers.Count,
        };
    }

    /// <summary>
    /// Returns the subscriptions expiring between now and the supplied horizon, ordered by their
    /// expiration date ascending.
    /// </summary>
    /// <param name="subscriptions">The subscription index records.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="horizonDays">The look-ahead horizon in days.</param>
    /// <returns>The expiring subscriptions.</returns>
    public static List<ExpiringSubscriptionRow> GetExpiringSubscriptions(
        IEnumerable<SubscriptionIndex> subscriptions,
        DateTime nowUtc,
        int horizonDays = 30)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var horizon = nowUtc.AddDays(horizonDays);

        return subscriptions
            .Where(subscription => subscription.ExpiresAt is not null &&
                subscription.ExpiresAt > nowUtc &&
                subscription.ExpiresAt <= horizon)
            .OrderBy(subscription => subscription.ExpiresAt)
            .Select(subscription => new ExpiringSubscriptionRow
            {
                OwnerId = subscription.OwnerId,
                ContentType = subscription.ContentType,
                StartedAt = subscription.StartedAt,
                ExpiresAt = subscription.ExpiresAt.Value,
                DaysRemaining = (int)Math.Ceiling((subscription.ExpiresAt.Value - nowUtc).TotalDays),
            })
            .ToList();
    }

    /// <summary>
    /// Buckets new subscriptions into chronological monthly counts based on their start date. Only
    /// subscriptions started within the inclusive period bounds are counted, and only months with data
    /// are returned.
    /// </summary>
    /// <param name="subscriptions">The subscription index records.</param>
    /// <param name="fromUtc">The inclusive lower bound, or <see langword="null"/> for no lower bound.</param>
    /// <param name="toUtc">The inclusive upper bound, or <see langword="null"/> for no upper bound.</param>
    /// <returns>The monthly new-subscription buckets, ordered from oldest to newest.</returns>
    public static List<MonthlySubscriptionBucket> BucketNewSubscriptionsByMonth(
        IEnumerable<SubscriptionIndex> subscriptions,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var from = fromUtc ?? DateTime.MinValue;
        var to = toUtc ?? DateTime.MaxValue;

        return subscriptions
            .Where(subscription => subscription.StartedAt >= from && subscription.StartedAt <= to)
            .GroupBy(subscription => StartOfMonth(subscription.StartedAt))
            .Select(group => new MonthlySubscriptionBucket
            {
                MonthStart = group.Key,
                Count = group.Count(),
            })
            .OrderBy(bucket => bucket.MonthStart)
            .ToList();
    }

    private static DateTime StartOfMonth(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}

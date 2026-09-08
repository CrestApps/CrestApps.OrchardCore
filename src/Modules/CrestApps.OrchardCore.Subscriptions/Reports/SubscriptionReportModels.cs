using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// Represents the headline revenue metrics computed for a set of succeeded transactions.
/// </summary>
public sealed class RevenueSummary
{
    /// <summary>
    /// Gets or sets the total gross revenue.
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded transactions.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Gets or sets the average value of a succeeded transaction.
    /// </summary>
    public decimal AverageTransactionValue { get; set; }

    /// <summary>
    /// Gets or sets the total tax collected.
    /// </summary>
    public decimal TotalTax { get; set; }
}

/// <summary>
/// Represents the revenue, tax, and transaction totals for a single calendar month.
/// </summary>
public sealed class MonthlyRevenueBucket
{
    /// <summary>
    /// Gets or sets the first day of the month, in UTC.
    /// </summary>
    public DateTime MonthStart { get; set; }

    /// <summary>
    /// Gets or sets the gross revenue for the month.
    /// </summary>
    public decimal Revenue { get; set; }

    /// <summary>
    /// Gets or sets the tax collected for the month.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded transactions for the month.
    /// </summary>
    public int TransactionCount { get; set; }
}

/// <summary>
/// Represents the count of new subscriptions started in a single calendar month.
/// </summary>
public sealed class MonthlySubscriptionBucket
{
    /// <summary>
    /// Gets or sets the first day of the month, in UTC.
    /// </summary>
    public DateTime MonthStart { get; set; }

    /// <summary>
    /// Gets or sets the number of subscriptions started in the month.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Represents the headline metrics shown on the subscriptions dashboard.
/// </summary>
public sealed class DashboardSummary
{
    /// <summary>
    /// Gets or sets the number of currently active subscriptions.
    /// </summary>
    public int ActiveSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of subscriptions started within the reporting period.
    /// </summary>
    public int NewSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of subscriptions expiring within the look-ahead horizon.
    /// </summary>
    public int ExpiringSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of agreements whose last payment failed and that are inside their grace period.
    /// </summary>
    public int PastDueSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of distinct subscribers.
    /// </summary>
    public int TotalSubscribers { get; set; }
}

/// <summary>
/// Represents a single subscription that is due to expire within the look-ahead horizon.
/// </summary>
public sealed class ExpiringSubscriptionRow
{
    /// <summary>
    /// Gets or sets the identifier of the subscriber that owns the subscription.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the title of the plan the subscriber agreed to.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the status the agreement is in.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the date the subscription started, in UTC.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the date the subscription expires, in UTC.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the whole number of days remaining until the subscription expires.
    /// </summary>
    public int DaysRemaining { get; set; }
}

/// <summary>
/// Represents the aggregated performance of a single subscription product (content type).
/// </summary>
public sealed class ProductPerformanceRow
{
    /// <summary>
    /// Gets or sets the identifier of the plan the payments were taken for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the plan's display title, falling back to its identifier when the plan no longer resolves.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded transactions for the product.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Gets or sets the gross revenue for the product.
    /// </summary>
    public decimal GrossRevenue { get; set; }

    /// <summary>
    /// Gets or sets the tax collected for the product.
    /// </summary>
    public decimal Tax { get; set; }
}

/// <summary>
/// Represents the confirmed payments taken in one currency.
/// </summary>
/// <remarks>
/// Revenue is reported per currency because adding amounts in different currencies produces a number that
/// means nothing.
/// </remarks>
public sealed class CurrencyRevenueGroup
{
    /// <summary>
    /// Gets or sets the ISO currency code, upper-cased.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the confirmed payments taken in this currency.
    /// </summary>
    public IReadOnlyList<PaymentAttemptIndex> Payments { get; set; } = [];
}

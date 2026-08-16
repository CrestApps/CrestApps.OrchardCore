using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Describes the billing cadence and optional limits for a subscription.
/// </summary>
public sealed class SubscriptionPlan
{
    /// <summary>
    /// Gets or sets the number of duration units in each billing cycle, such as 1 year, 30 days, or 4 weeks.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit used by <see cref="BillingDuration"/>.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of billing cycles to process, or <see langword="null"/> for no limit.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets the number of days to delay before the subscription starts.
    /// </summary>
    public int? SubscriptionDayDelay { get; set; }
}

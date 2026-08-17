using CrestApps.OrchardCore.Payments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents subscription billing configuration attached to a subscription content item.
/// </summary>
public sealed class SubscriptionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the line item description for the initial one-time amount.
    /// </summary>
    public string InitialAmountDescription { get; set; }

    /// <summary>
    /// Gets or sets the one-time amount charged when the subscription starts.
    /// </summary>
    public decimal? InitialAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of <see cref="DurationType"/> units in one billing cycle.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit used with <see cref="BillingDuration"/> to define the billing cycle length.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of billing cycles to process before the subscription ends.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets the number of days to delay the first recurring subscription payment.
    /// </summary>
    public int? SubscriptionDayDelay { get; set; }

    /// <summary>
    /// Gets or sets the sort position for displaying the subscription.
    /// </summary>
    public int? Sort { get; set; }
}

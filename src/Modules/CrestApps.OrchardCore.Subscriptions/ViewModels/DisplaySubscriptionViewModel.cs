using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscription price and billing terms shown to a customer.
/// </summary>
public class DisplaySubscriptionViewModel
{
    /// <summary>
    /// Gets or sets the recurring subscription price.
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Gets or sets the display text that describes the initial amount.
    /// </summary>
    public string InitialAmountDescription { get; set; }

    /// <summary>
    /// Gets or sets the optional amount charged when the subscription starts.
    /// </summary>
    public double? InitialAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of duration units between recurring charges.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit used by the billing duration.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of billing cycles for the subscription.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets the optional delay, in days, before the first subscription billing cycle starts.
    /// </summary>
    public int? SubscriptionDayDelay { get; set; }
}

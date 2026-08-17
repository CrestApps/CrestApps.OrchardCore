using CrestApps.OrchardCore.Payments.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents editable subscription billing configuration for a subscription content item.
/// </summary>
public class SubscriptionPartViewModel
{
    /// <summary>
    /// Gets or sets the one-time amount charged when the subscription starts.
    /// </summary>
    public decimal? InitialAmount { get; set; }

    /// <summary>
    /// Gets or sets the line item description for the initial one-time amount.
    /// </summary>
    public string InitialAmountDescription { get; set; }

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
    /// Gets or sets the available billing duration units.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DurationTypes { get; set; }
}

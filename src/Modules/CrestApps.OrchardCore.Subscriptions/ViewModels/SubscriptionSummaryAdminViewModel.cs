using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscription session summary displayed in the admin UI.
/// </summary>
public sealed class SubscriptionSummaryAdminViewModel
{
    /// <summary>
    /// Gets or sets the unique identifier of the subscription session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle status of the subscription session.
    /// </summary>
    public SubscriptionSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the display name of the customer associated with the session.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the customer associated with the session.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the title of the subscription plan being purchased.
    /// </summary>
    public string PlanTitle { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used by the subscription session.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount currently due for the subscription session.
    /// </summary>
    public double DueNow { get; set; }

    /// <summary>
    /// Gets or sets the one-time amount charged when the subscription starts.
    /// </summary>
    public double? InitialAmount { get; set; }

    /// <summary>
    /// Gets or sets the recurring amount charged for each billing cycle.
    /// </summary>
    public double? RecurringAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of <see cref="DurationType"/> units in one billing cycle.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit used with <see cref="BillingDuration"/> to define the billing cycle length.
    /// </summary>
    public DurationType? DurationType { get; set; }
}

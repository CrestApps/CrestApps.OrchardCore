using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Describes a recurring billing plan attached to a checkout line item. A line item with a plan is
/// billed every cycle; a line item without one is a single up-front charge.
/// </summary>
public sealed class RecurringPlan
{
    /// <summary>
    /// The number of <see cref="DurationType"/> units in one billing cycle (for example <c>1</c> year or <c>30</c> days).
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// The unit of time that <see cref="BillingDuration"/> is expressed in.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// An optional limit on how many billing cycles are charged before billing stops.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// An optional number of days to delay the start of the recurring billing.
    /// </summary>
    public int? StartDayDelay { get; set; }
}

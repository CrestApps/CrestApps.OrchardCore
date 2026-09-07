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

    /// <summary>
    /// An optional free trial, in days, before the first cycle is billed.
    /// </summary>
    /// <remarks>
    /// It is deliberately separate from <see cref="StartDayDelay"/>. A delayed start postpones the agreement
    /// itself; a trial establishes the agreement now, with a payment method attached, and simply does not
    /// charge until the trial ends. That difference is what lets a trial convert into a paying subscriber
    /// without asking them to come back and buy again.
    /// </remarks>
    public int? TrialDays { get; set; }
}

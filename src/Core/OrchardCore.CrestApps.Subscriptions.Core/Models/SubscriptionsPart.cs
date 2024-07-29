using OrchardCore.ContentManagement;

namespace OrchardCore.CrestApps.Subscriptions.Core.Models;

public class SubscriptionsPart : ContentPart
{
    /// <summary>
    /// Initial Payment amount to apply.
    /// </summary>
    public double? InitialAmount { get; set; }

    /// <summary>
    /// The billing amount to collect every billing cycle.
    /// </summary>
    public double BillingAmount { get; set; }

    /// <summary>
    /// The number of payments pet duration type.
    /// For example, 1 Year, 30 Days, 4 Weeks, etc.
    /// <see cref="DurationType"/> to define the duration limit.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// The duration type for <see cref="BillingDuration"/>.
    /// When <see cref="BillingDuration"/> is set to 1 and type is Year,
    /// This means 1 year billing cycle.
    /// </summary>
    public BillingDurationType DurationType { get; set; }

    /// <summary>
    /// You can set a limit on how many payment cycles to process.
    /// For example, 4 would be 4 payment cycle and after that no further payments will be processed.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    ///  Number of days to delay the start of the subscription.
    /// </summary>
    public int? SubscriptionDayDelay { get; set; }

    /// <summary>
    /// The position the subscription should be sorted by.
    /// </summary>
    public int? Sort { get; set; }
}

public enum BillingDurationType
{
    Year,
    Month,
    Week,
    Day,
}

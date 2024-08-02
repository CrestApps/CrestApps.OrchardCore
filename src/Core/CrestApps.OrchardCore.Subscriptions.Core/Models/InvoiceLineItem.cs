namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class InvoiceLineItem
{
    /// <summary>
    /// Short description of the line item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The items quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The price of each unit.
    /// </summary>
    public double UnitPrice { get; set; }

    /// <summary>
    /// The line item subtotal. The subtotal will be calculated by multiplying <see cref="Quantity"/> with <see cref="UnitPrice"/>.
    /// </summary>
    public double Subtotal { get; set; }

    /// <summary>
    /// Any amount that should be paid now.
    /// </summary>
    public double? DueNow { get; set; }

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
}

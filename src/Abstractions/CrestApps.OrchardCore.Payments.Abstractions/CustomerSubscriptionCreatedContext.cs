namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a customer subscription that was created by a payment gateway.
/// </summary>
public sealed class CustomerSubscriptionCreatedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the payment gateway identifier of the subscription plan.
    /// </summary>
    public string PlanId { get; set; }

    /// <summary>
    /// Gets or sets the amount charged by the subscription plan, when the gateway provides it.
    /// </summary>
    public double? PlanAmount { get; set; }

    /// <summary>
    /// Gets or sets the currency used by the subscription plan.
    /// </summary>
    public string PlanCurrency { get; set; }

    /// <summary>
    /// Gets or sets the recurring interval configured for the subscription plan.
    /// </summary>
    public string PlanInterval { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway identifier of the created subscription.
    /// </summary>
    public string SubscriptionId { get; set; }
}

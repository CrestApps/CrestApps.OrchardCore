namespace CrestApps.OrchardCore.Payments;

public sealed class CustomerSubscriptionCreatedContext : PaymentEventContextBase
{
    public string PlanId { get; set; }

    public double? PlanAmount { get; set; }

    public string PlanCurrency { get; set; }

    public string PlanInterval { get; set; }
}
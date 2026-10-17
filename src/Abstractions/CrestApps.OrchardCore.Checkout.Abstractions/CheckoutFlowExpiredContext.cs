namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context of a checkout nobody finished, closed after the checkout lifetime.
/// </summary>
public sealed class CheckoutFlowExpiredContext : CheckoutFlowContextBase
{
    public CheckoutFlowExpiredContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

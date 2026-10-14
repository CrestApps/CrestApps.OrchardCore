namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised after a new checkout session has been activated. The checkout invoice is built here.
/// </summary>
public sealed class CheckoutFlowActivatedContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowActivatedContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowActivatedContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

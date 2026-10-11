namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised before a checkout session is initialized for display.
/// </summary>
public sealed class CheckoutFlowInitializingContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowInitializingContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowInitializingContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

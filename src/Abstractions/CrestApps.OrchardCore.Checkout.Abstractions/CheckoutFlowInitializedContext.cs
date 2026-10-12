namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised after a checkout session has been initialized for display.
/// </summary>
public sealed class CheckoutFlowInitializedContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowInitializedContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowInitializedContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

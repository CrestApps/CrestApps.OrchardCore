namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised after a checkout session has been loaded for the current step.
/// </summary>
public sealed class CheckoutFlowLoadedContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowLoadedContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowLoadedContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

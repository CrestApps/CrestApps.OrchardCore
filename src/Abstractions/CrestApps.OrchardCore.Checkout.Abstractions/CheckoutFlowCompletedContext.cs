namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised after a checkout has completed and every obligation has been fulfilled.
/// </summary>
public sealed class CheckoutFlowCompletedContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowCompletedContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowCompletedContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

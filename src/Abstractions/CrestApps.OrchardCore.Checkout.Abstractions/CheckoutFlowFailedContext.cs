namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised when a checkout fails.
/// </summary>
public sealed class CheckoutFlowFailedContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowFailedContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowFailedContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

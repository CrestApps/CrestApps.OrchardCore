namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised before a checkout session is loaded for the current step.
/// </summary>
public sealed class CheckoutFlowLoadingContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowLoadingContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowLoadingContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

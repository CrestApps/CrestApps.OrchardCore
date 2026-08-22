namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised before a checkout is completed, after every step and payment has been validated.
/// </summary>
public sealed class CheckoutFlowCompletingContext : CheckoutFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowCompletingContext"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    public CheckoutFlowCompletingContext(CheckoutFlow flow)
        : base(flow)
    {
    }
}

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Base class for checkout flow lifecycle contexts that expose the active <see cref="CheckoutFlow"/>.
/// </summary>
public abstract class CheckoutFlowContextBase
{
    /// <summary>
    /// The flow the event is being raised for.
    /// </summary>
    public CheckoutFlow Flow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowContextBase"/> class.
    /// </summary>
    /// <param name="flow">The active checkout flow.</param>
    protected CheckoutFlowContextBase(CheckoutFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        Flow = flow;
    }
}

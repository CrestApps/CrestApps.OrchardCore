namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context of a checkout that was canceled before it was completed.
/// </summary>
public sealed class CheckoutFlowCanceledContext : CheckoutFlowContextBase
{
    public CheckoutFlowCanceledContext(CheckoutFlow flow, string reason)
        : base(flow)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets why the checkout was canceled, as the caller gave it.
    /// </summary>
    public string Reason { get; }
}

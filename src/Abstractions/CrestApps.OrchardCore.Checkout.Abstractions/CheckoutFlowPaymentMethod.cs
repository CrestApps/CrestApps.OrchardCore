namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The shape rendered for each registered payment method during the payment step. Payment provider
/// features contribute a display driver for this type to render their method-specific UI.
/// </summary>
public sealed class CheckoutFlowPaymentMethod
{
    /// <summary>
    /// The flow the payment method is being rendered for.
    /// </summary>
    public CheckoutFlow Flow { get; set; }
}

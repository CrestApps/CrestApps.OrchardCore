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

    /// <summary>
    /// Gets or sets a value indicating whether the page asks the provider to keep the payment method so it can be
    /// charged again later without the payer present (see <c>CheckoutPaymentDataKeys.SavePaymentMethod</c>). A
    /// provider panel that honors it tokenizes a reusable payment method before the payment begins.
    /// </summary>
    public bool SavePaymentMethod { get; set; }
}

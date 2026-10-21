namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The model behind the page shown for a checkout that can no longer be paid, because it was canceled, expired or
/// failed. It is the model of the <c>CheckoutEnded</c> shape, so it stays unsealed for the shape's proxy.
/// </summary>
public class CheckoutEndedViewModel
{
    /// <summary>
    /// Gets or sets the checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the state the checkout ended in.
    /// </summary>
    public CheckoutSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the local URL the customer goes back to, as the module that started the checkout asked, or
    /// <see langword="null"/> when it named none.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the text of the link to <see cref="ReturnUrl"/>, or <see langword="null"/> for <b>Back</b>.
    /// </summary>
    public string ReturnText { get; set; }
}

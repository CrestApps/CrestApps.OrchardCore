using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The model behind the public checkout page. The step content is a shape built by the registered step
/// drivers, so the page itself stays agnostic of what any particular checkout collects.
/// </summary>
public sealed class CheckoutViewModel
{
    /// <summary>
    /// Gets or sets the checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the step being displayed.
    /// </summary>
    public string Step { get; set; }

    /// <summary>
    /// Gets or sets the rendered step content.
    /// </summary>
    public IShape Content { get; set; }

    /// <summary>
    /// Gets or sets the local URL the customer returns to, as the module that started the checkout asked. The
    /// confirmation page shows a <b>Continue</b> button to it.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the text of the link to <see cref="ReturnUrl"/>, or <see langword="null"/> for the page's own
    /// wording.
    /// </summary>
    public string ReturnText { get; set; }

    /// <summary>
    /// Gets or sets the heading of the confirmation page, or <see langword="null"/> for "Order confirmation".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets whether the checkout was completed with a method that takes the money later, such as a bank
    /// transfer, so the confirmation says the order is placed rather than paid.
    /// </summary>
    public bool PaymentDeferred { get; set; }
}

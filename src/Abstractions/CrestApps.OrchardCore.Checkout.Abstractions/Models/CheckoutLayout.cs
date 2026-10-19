namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// How the checkout's steps are laid out for the buyer.
/// </summary>
public enum CheckoutLayout
{
    /// <summary>
    /// A page per step, with a progress bar.
    /// </summary>
    Steps,

    /// <summary>
    /// Every step on one page: the details first, then payment once they are complete.
    /// </summary>
    OnePage,
}

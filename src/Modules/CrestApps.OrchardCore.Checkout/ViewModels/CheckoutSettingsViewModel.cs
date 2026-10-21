using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The checkout settings editor.
/// </summary>
public class CheckoutSettingsViewModel
{
    /// <summary>
    /// Gets or sets how the checkout's steps are laid out.
    /// </summary>
    public CheckoutLayout Layout { get; set; }
}

using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Customers.ViewModels;

/// <summary>
/// The store's checkout fields: their editor on the Contact step, or what was entered on an order's page.
/// </summary>
public class CheckoutDetailsViewModel
{
    /// <summary>
    /// Gets or sets the Checkout details item's editor or display shape.
    /// </summary>
    public IShape Content { get; set; }
}

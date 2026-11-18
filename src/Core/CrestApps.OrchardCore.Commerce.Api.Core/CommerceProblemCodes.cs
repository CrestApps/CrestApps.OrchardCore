using CrestApps.OrchardCore.Api;
namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// The conflict codes of the commerce API, beside the shared ones of <see cref="CrestApps.OrchardCore.Api.ApiProblems.Codes"/>.
/// </summary>
public static class CommerceProblemCodes
{
    /// <summary>
    /// The cart has nothing in it.
    /// </summary>
    public const string EmptyCart = "empty-cart";

    /// <summary>
    /// An item is out of stock.
    /// </summary>
    public const string OutOfStock = "out-of-stock";

    /// <summary>
    /// The invoice changed since the caller read it.
    /// </summary>
    public const string InvoiceChanged = "invoice-changed";

    /// <summary>
    /// A payment begun for the old total can't be canceled at the provider (it was taken, or the provider refused), so
    /// the change that would alter the total is refused.
    /// </summary>
    public const string PaymentInProgress = "payment-in-progress";
}

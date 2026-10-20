using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// Gives cart lines details beside the product, such as a departure date and the travelers on it, or the add-ons
/// chosen: checks them when the line is added, prices and describes them whenever the cart is priced, and they are
/// copied onto the order line. Two lines of the same product with other details stay apart.
/// </summary>
/// <remarks>
/// Every handler is asked about every line, so one that doesn't deal with a product leaves it alone. Product add-ons
/// are the first handler, and always run.
/// </remarks>
public interface ICartLineHandler
{
    /// <summary>
    /// Gets when the handler runs, lowest first. One that sets <see cref="CartLineEvaluateContext.PeoplePerUnit"/> runs
    /// before add-ons (100), whose prices can be per person.
    /// </summary>
    int Order
        => 0;

    /// <summary>
    /// Keeps the details the buyer gave that the handler deals with, checked and in their stored form, or refuses the
    /// line. What no handler keeps is dropped.
    /// </summary>
    Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prices and checks a line as it stands now: changes its unit price, limits how many can be bought, describes its
    /// details, or says why it can't be bought.
    /// </summary>
    Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A product being added to a cart, with what the buyer chose for it.
/// </summary>
public sealed class CartLinePrepareContext
{
    public ContentItem ContentItem { get; init; }

    /// <summary>
    /// Gets the product's part, or <see langword="null"/> for an item sold without one.
    /// </summary>
    public ProductPart ProductPart { get; init; }

    public string Title { get; init; }

    public string VariantId { get; init; }

    public int Quantity { get; init; }

    /// <summary>
    /// Gets the add-ons the buyer chose, by add-on id, as they were sent.
    /// </summary>
    public IReadOnlyDictionary<string, string> PostedAddOns { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the details the buyer gave, by key, as they were sent.
    /// </summary>
    public IReadOnlyDictionary<string, string> PostedDetails { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the add-ons kept for the line.
    /// </summary>
    public Dictionary<string, string> AddOns { get; set; }

    /// <summary>
    /// Gets the details kept for the line, by key. A handler names its keys after itself, such as <c>booking.date</c>.
    /// </summary>
    public Dictionary<string, string> Details { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets why the line can't be added, or <see langword="null"/>.
    /// </summary>
    public LocalizedString Problem { get; private set; }

    /// <summary>
    /// Refuses the line: the buyer is told why, and nothing is added.
    /// </summary>
    public void Refuse(LocalizedString problem)
        => Problem ??= problem;
}

/// <summary>
/// A cart line being priced: the product's price for it, which handlers change for the line's details.
/// </summary>
public sealed class CartLineEvaluateContext
{
    private readonly Func<decimal, CancellationToken, Task<decimal>> _convert;

    public CartLineEvaluateContext(Func<decimal, CancellationToken, Task<decimal>> convert = null)
    {
        _convert = convert;
    }

    public CartLine Line { get; init; }

    public ContentItem ContentItem { get; init; }

    public ProductPart ProductPart { get; init; }

    public ISellableProduct Product { get; init; }

    public PriceResult Price { get; init; }

    public string Title { get; init; }

    /// <summary>
    /// Gets the cart the line is in, or <see langword="null"/> for one not saved yet, so what its own unpaid orders
    /// hold can count as available to it.
    /// </summary>
    public string CartId { get; init; }

    /// <summary>
    /// Gets how many are being priced: the line's quantity, or what it would be.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Gets or sets the price of one unit, in the line's currency. It starts at the product's price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the most units that can be bought.
    /// </summary>
    public int MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets how many people one unit is for, such as two for a double room. Add-ons priced per person are
    /// charged for each.
    /// </summary>
    public int PeoplePerUnit { get; set; } = 1;

    /// <summary>
    /// Gets the add-ons chosen, as they read.
    /// </summary>
    public List<ProductAddOnChoice> AddOns { get; } = [];

    /// <summary>
    /// Gets the line's details as they read, such as "Departure: 1 December 2026".
    /// </summary>
    public List<CartLineDetail> Details { get; } = [];

    /// <summary>
    /// Gets or sets why the line can't be bought as it is, or <see langword="null"/>.
    /// </summary>
    public LocalizedString Problem { get; set; }

    /// <summary>
    /// Converts an amount in the product's own currency into the line's, as a price.
    /// </summary>
    public Task<decimal> ConvertAsync(decimal amount, CancellationToken cancellationToken = default)
        => amount == 0m || _convert is null ? Task.FromResult(amount) : _convert(amount, cancellationToken);

    /// <summary>
    /// Reads one of the line's details, or <see langword="null"/>.
    /// </summary>
    public string GetDetail(string key)
        => Line?.Details is { } details && details.TryGetValue(key, out var value) ? value : null;
}

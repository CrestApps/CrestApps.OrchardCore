using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Carts.Core.Models;

/// <summary>
/// A cart priced now: every line at today's price, and what stands in the way of buying it.
/// </summary>
public sealed class CartPricing
{
    public Cart Cart { get; init; }

    public string Currency { get; init; }

    public IReadOnlyList<CartLinePricing> Lines { get; init; } = [];

    /// <summary>
    /// Gets the total of the lines that can be bought, before discounts and tax, which the checkout works out.
    /// </summary>
    public decimal Subtotal
        => Lines.Where(line => line.IsAvailable).Sum(line => line.LineTotal);

    /// <summary>
    /// Gets the number of units in the cart.
    /// </summary>
    public int ItemCount
        => Lines.Sum(line => line.Line.Quantity);

    /// <summary>
    /// Gets whether the cart can be bought: it has lines, and every one of them can be bought as it is.
    /// </summary>
    public bool CanCheckout
        => Lines.Count > 0 && Lines.All(line => line.IsAvailable);
}

/// <summary>
/// One cart line priced now.
/// </summary>
public sealed class CartLinePricing
{
    /// <summary>
    /// Gets or sets the add-ons chosen for the line, as they read: label, the text typed (or nothing for a ticked
    /// box) and the extra price per unit, which <see cref="UnitPrice"/> includes.
    /// </summary>
    public IReadOnlyList<ProductAddOnChoice> AddOns { get; set; } = [];

    /// <summary>
    /// Gets or sets the line's details as they read, such as "Departure: 1 December 2026".
    /// </summary>
    public IReadOnlyList<CartLineDetail> Details { get; set; } = [];

    public CartLine Line { get; init; }

    /// <summary>
    /// Gets the published product, or <see langword="null"/> when it is gone.
    /// </summary>
    public ContentItem ContentItem { get; init; }

    public ISellableProduct Product { get; init; }

    public PriceResult Price { get; init; }

    public string Title { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }

    /// <summary>
    /// Gets the most units of the line that can be bought.
    /// </summary>
    public int MaxQuantity { get; init; }

    /// <summary>
    /// Gets when a pre-ordered line ships, or <see langword="null"/>.
    /// </summary>
    public DateTime? ShipsFromUtc { get; init; }

    /// <summary>
    /// Gets whether the price is not the one shown when the line was added.
    /// </summary>
    public bool PriceChanged { get; init; }

    /// <summary>
    /// Gets why the line cannot be bought as it is, or <see langword="null"/> when it can.
    /// </summary>
    public LocalizedString Problem { get; init; }

    public bool IsAvailable
        => Problem is null;
}

/// <summary>
/// One of a line's details as it reads: what it is, and what was chosen.
/// </summary>
public sealed record CartLineDetail(string Key, string Label, string Value);

/// <summary>
/// The outcome of changing a cart.
/// </summary>
public sealed class CartResult
{
    public bool Succeeded { get; init; }

    public Cart Cart { get; init; }

    /// <summary>
    /// Gets what to tell the visitor: what was done, or why it was not.
    /// </summary>
    public LocalizedString Message { get; init; }

    public static CartResult Success(Cart cart, LocalizedString message)
        => new() { Succeeded = true, Cart = cart, Message = message };

    public static CartResult Failure(LocalizedString message, Cart cart = null)
        => new() { Succeeded = false, Cart = cart, Message = message };
}

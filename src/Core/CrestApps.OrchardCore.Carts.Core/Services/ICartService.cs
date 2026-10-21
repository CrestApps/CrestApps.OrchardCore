using CrestApps.OrchardCore.Carts.Core.Models;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// The current visitor's cart: finding it, changing it, and pricing it.
/// </summary>
public interface ICartService
{
    /// <summary>
    /// Gets the current visitor's cart, or <see langword="null"/> when they have none or it expired.
    /// </summary>
    Task<Cart> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds units of a product at one of its prices, or more units to the line that already has it. The product
    /// must be published and sold online, the price one-time and in the cart's currency, and the quantity one
    /// the price allows.
    /// </summary>
    /// <param name="contentItemId">The product.</param>
    /// <param name="priceId">The product's price, or <see langword="null"/> for its default price.</param>
    /// <param name="quantity">How many to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CartResult> AddAsync(string contentItemId, string priceId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds units of one variant of a product (a size, a color), or more units to the line that already has it.
    /// A product sold in variants can only be added as one of them.
    /// </summary>
    /// <param name="contentItemId">The product.</param>
    /// <param name="priceId">The product's price, or <see langword="null"/> for its default price.</param>
    /// <param name="variantId">The variant, or <see langword="null"/> for a product without variants.</param>
    /// <param name="quantity">How many to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CartResult> AddAsync(string contentItemId, string priceId, string variantId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds units of a product with the add-ons the buyer chose, such as an engraving. The same product with other
    /// add-ons is another line.
    /// </summary>
    /// <param name="contentItemId">The product.</param>
    /// <param name="priceId">The product's price, or <see langword="null"/> for its default price.</param>
    /// <param name="variantId">The variant, or <see langword="null"/> for a product without variants.</param>
    /// <param name="quantity">How many to add.</param>
    /// <param name="addOns">The add-ons chosen, by add-on id: the text typed, or <c>true</c> for a ticked box.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CartResult> AddAsync(string contentItemId, string priceId, string variantId, int quantity, IReadOnlyDictionary<string, string> addOns, CancellationToken cancellationToken = default)
        => AddAsync(contentItemId, priceId, variantId, quantity, cancellationToken);

    /// <summary>
    /// Changes how many units a line has. A quantity below one removes the line.
    /// </summary>
    Task<CartResult> UpdateQuantityAsync(string lineId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a line.
    /// </summary>
    Task<CartResult> RemoveAsync(string lineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cart, such as one that was just bought.
    /// </summary>
    Task DeleteAsync(Cart cart, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the guest cart of this browser into a user's cart when they sign in.
    /// </summary>
    Task MergeGuestCartAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prices every line of a cart now.
    /// </summary>
    Task<CartPricing> PriceAsync(Cart cart, CancellationToken cancellationToken = default);
}

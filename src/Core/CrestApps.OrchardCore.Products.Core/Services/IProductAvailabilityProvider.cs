namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Says how many units of products are available to buy, for modules that count stock. A product the provider doesn't
/// count is left out of the answer, and can always be bought.
/// </summary>
public interface IProductAvailabilityProvider
{
    Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A product, or one of its variants (<see langword="null"/> for a product without variants).
/// </summary>
public readonly record struct ProductStockKey(string ContentItemId, string VariantId);

public sealed class ProductAvailabilityRequest
{
    public IReadOnlyCollection<ProductStockKey> Products { get; init; } = [];

    /// <summary>
    /// Gets the cart asking, if any: units its own unpaid orders already hold count as available to it, so a buyer
    /// who started checkout and came back isn't told their own units are gone.
    /// </summary>
    public string CartId { get; init; }
}

/// <summary>
/// How many units can be bought, and whether more can be sold as backorders.
/// </summary>
public sealed record ProductAvailability(int Available, bool AllowBackorder, int LowStockThreshold)
{
    /// <summary>
    /// Gets when a product not out yet ships, which makes a backorder a pre-order, or <see langword="null"/>. Providers
    /// set it only while the date is to come.
    /// </summary>
    public DateTime? ReleaseDateUtc { get; init; }

    public ProductAvailabilityStatus Status
        => Available > 0
            ? Available <= LowStockThreshold ? ProductAvailabilityStatus.LowStock : ProductAvailabilityStatus.InStock
            : AllowBackorder
                ? ReleaseDateUtc.HasValue ? ProductAvailabilityStatus.Preorder : ProductAvailabilityStatus.Backorder
                : ProductAvailabilityStatus.OutOfStock;

    public bool CanBuy(int quantity)
        => AllowBackorder || Available >= quantity;
}

public enum ProductAvailabilityStatus
{
    InStock = 0,

    /// <summary>
    /// In stock, at or below the low-stock threshold, so the buyer is told how many are left.
    /// </summary>
    LowStock = 1,

    OutOfStock = 2,

    /// <summary>
    /// None left, but it can still be ordered.
    /// </summary>
    Backorder = 3,

    /// <summary>
    /// Not out yet, and ordered ahead: none in stock, backorders taken, with a release date to come.
    /// </summary>
    Preorder = 4,
}

public static class ProductAvailabilityProviderExtensions
{
    /// <summary>
    /// Asks every provider and keeps the first answer for each product.
    /// </summary>
    public static async Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(
        this IEnumerable<IProductAvailabilityProvider> providers,
        IReadOnlyCollection<ProductStockKey> products,
        string cartId = null,
        CancellationToken cancellationToken = default)
    {
        var answer = new Dictionary<ProductStockKey, ProductAvailability>();

        if (products is not { Count: > 0 })
        {
            return answer;
        }

        var request = new ProductAvailabilityRequest { Products = products, CartId = cartId };

        foreach (var provider in providers ?? [])
        {
            foreach (var (key, availability) in await provider.GetAvailabilityAsync(request, cancellationToken))
            {
                answer.TryAdd(key, availability);
            }
        }

        return answer;
    }

    /// <summary>
    /// What a product with variants offers as a whole: available when any variant is, with the units of all of them.
    /// </summary>
    public static ProductAvailability Combine(IEnumerable<ProductAvailability> variants)
    {
        var list = variants?.Where(availability => availability is not null).ToArray() ?? [];

        if (list.Length == 0)
        {
            return null;
        }

        return new ProductAvailability(
            list.Sum(availability => Math.Max(0, availability.Available)),
            list.Any(availability => availability.AllowBackorder),
            list.Max(availability => availability.LowStockThreshold));
    }
}

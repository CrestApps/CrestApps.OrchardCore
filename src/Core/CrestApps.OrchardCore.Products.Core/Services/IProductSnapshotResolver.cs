using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Resolves a product content item into a provider-neutral <see cref="ISellableProduct"/> snapshot.
/// Checkout, payment, and future ordering code depend on this seam instead of reading a product's parts
/// directly, so the catalog can evolve (variants, price schedules, richer identity) without changing the
/// payment or checkout modules.
/// </summary>
public interface IProductSnapshotResolver
{
    /// <summary>
    /// Resolves the supplied context into a sellable product snapshot, or <see langword="null"/> when the
    /// content item is not a resolvable, sellable product.
    /// </summary>
    /// <param name="context">The resolution context carrying the content item and selling options.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ISellableProduct> ResolveAsync(ProductSnapshotContext context, CancellationToken cancellationToken = default);
}

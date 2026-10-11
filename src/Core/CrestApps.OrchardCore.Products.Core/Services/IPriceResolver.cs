using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Resolves the effective price of a product for a selling context into a currency-tagged
/// <see cref="PriceResult"/>. Checkout, payment, and future ordering code depend on this seam instead of
/// reading a product's raw price, so pricing rules can evolve (price schedules, quantity breaks, or
/// customer-specific pricing) without changing those flows. A product owns its currency; this resolver
/// never converts between currencies.
/// </summary>
public interface IPriceResolver
{
    /// <summary>
    /// Resolves the effective price for the supplied context, or <see langword="null"/> when the content
    /// item is not a priceable product or cannot be priced in the requested currency (a currency mismatch
    /// is rejected, never converted).
    /// </summary>
    /// <param name="context">The resolution context carrying the content item and selling options.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PriceResult> ResolveAsync(ProductSnapshotContext context, CancellationToken cancellationToken = default);
}

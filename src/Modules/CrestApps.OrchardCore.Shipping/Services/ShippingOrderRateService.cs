using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Prices shipping for an order: its goods, with the weight and shipping class of the product version bought.
/// </summary>
public sealed class ShippingOrderRateService
{
    private readonly IShippingRateService _rateService;
    private readonly IContentManager _contentManager;

    public ShippingOrderRateService(IShippingRateService rateService, IContentManager contentManager)
    {
        _rateService = rateService;
        _contentManager = contentManager;
    }

    /// <summary>
    /// Gets whether anything in the order ships.
    /// </summary>
    public static bool Ships(Order order)
        => order?.Lines?.Any(IsPhysical) == true;

    public static bool IsPhysical(OrderLine line)
        => line.ProductType == ProductType.Good && line.Quantity > 0;

    public async Task<ShippingRateRequest> BuildRequestAsync(Order order, Address destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var lines = new List<ShippingRequestLine>();
        var products = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

        foreach (var line in order.Lines.Where(IsPhysical))
        {
            var key = line.ContentItemVersionId ?? line.ContentItemId;

            if (!products.TryGetValue(key, out var product))
            {
                // The version bought, so a later change to the product doesn't change what this order weighs.
                product = (string.IsNullOrEmpty(line.ContentItemVersionId) ? null : await _contentManager.GetVersionAsync(line.ContentItemVersionId))
                    ?? await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Latest);

                products[key] = product;
            }

            ShippingPart part = null;
            product?.TryGet(out part);

            lines.Add(new ShippingRequestLine
            {
                ContentItemId = line.ContentItemId,
                VariantId = line.VariantId,
                Title = line.Title,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Weight = part?.GetWeight(line.VariantId) ?? 0,
                ShippingClass = part?.ShippingClass,
            });
        }

        return new ShippingRateRequest
        {
            Destination = destination,
            Currency = order.Currency,
            Lines = lines,
        };
    }

    public async Task<ShippingQuote> QuoteAsync(Order order, Address destination, CancellationToken cancellationToken = default)
    {
        var request = await BuildRequestAsync(order, destination, cancellationToken);
        var zone = await _rateService.FindZoneAsync(destination, cancellationToken);

        return new ShippingQuote(zone, zone is null ? [] : await _rateService.GetRatesAsync(request, cancellationToken));
    }
}

/// <summary>
/// The zone an address is in, and the rates its methods offer for an order.
/// </summary>
public sealed record ShippingQuote(ShippingZone Zone, IReadOnlyList<ShippingRate> Rates);

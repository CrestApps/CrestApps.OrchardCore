using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Turns a priced cart into an order's lines, frozen at the prices the cart was priced at.
/// </summary>
public static class OrderLines
{
    public static List<OrderLine> FromPricing(CartPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        return pricing.Lines.Select(line => new OrderLine
        {
            LineId = line.Line.LineId,
            ContentItemId = line.Line.ContentItemId,
            ContentItemVersionId = line.Product?.ContentItemVersionId ?? line.ContentItem?.ContentItemVersionId,
            ContentType = line.Product?.ContentType ?? line.ContentItem?.ContentType,
            Title = line.Title,
            Sku = line.Product?.Sku,
            PriceId = line.Price?.PriceId,
            PriceName = line.Price?.Price?.Name,
            VariantId = line.Line.VariantId,
            VariantTitle = line.Product?.VariantTitle,
            Quantity = line.Line.Quantity,
            UnitPrice = line.UnitPrice,
            LineTotal = line.LineTotal,
            ProductType = line.Product?.ProductType ?? default,
            TaxCategoryCode = line.Product?.TaxCategoryCode,
            TaxClassificationCode = line.Product?.TaxClassificationCode,
            CategoryIds = ProductCategories.GetIds(line.ContentItem),
            AddOns = [.. (line.AddOns ?? []).Select(addOn => new OrderLineAddOn { AddOnId = addOn.AddOnId, Label = addOn.Label, Value = addOn.Value, Price = addOn.Price })],
            ShipsFromUtc = line.ShipsFromUtc,
            BundleItems = line.ContentItem?.TryGet<ProductBundlePart>(out var bundle) == true && bundle.Items is { Count: > 0 }
                ? [.. bundle.Items.Where(item => !string.IsNullOrEmpty(item.ContentItemId)).Select(item => new OrderLineBundleItem { ContentItemId = item.ContentItemId, Title = item.Title, Quantity = Math.Max(1, item.Quantity) })]
                : [],
        }).ToList();
    }
}

using System.Data;
using System.Globalization;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.ContentTransfer;

/// <summary>
/// Imports and exports a product's price, compare-at and sale prices with the sale's dates, SKU, currency, the address
/// of an external product, and its quantity rules. A cell left empty leaves the value as it was.
/// </summary>
public sealed class ProductPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    internal readonly IStringLocalizer S;

    public ProductPartContentImportHandler(IStringLocalizer<ProductPartContentImportHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        var prefix = (context?.ContentTypePartDefinition?.Name ?? nameof(ProductPart)) + "_";

        return
        [
            new() { Name = prefix + nameof(ProductPart.Price), Description = S["The price, such as 19.99."], AdditionalNames = ["Price"] },
            new() { Name = prefix + nameof(ProductPart.CompareAtPrice), Description = S["The price it was, shown struck through."], AdditionalNames = ["Compare-at price", "CompareAtPrice"] },
            new() { Name = prefix + nameof(ProductPart.SalePrice), Description = S["The price during a sale."], AdditionalNames = ["Sale price"] },
            new() { Name = prefix + nameof(ProductPart.SaleStartsUtc), Description = S["When the sale starts, in UTC, such as 2026-11-27 08:00."] },
            new() { Name = prefix + nameof(ProductPart.SaleEndsUtc), Description = S["When the sale ends, in UTC."] },
            new() { Name = prefix + nameof(ProductPart.Sku), Description = S["The product's SKU."], AdditionalNames = ["SKU"] },
            new() { Name = prefix + nameof(ProductPart.Currency), Description = S["The currency code, such as USD. Empty uses the type's default."], AdditionalNames = ["Currency"] },
            new() { Name = prefix + nameof(ProductPart.ExternalUrl), Description = S["Where an external product is sold, instead of here."] },
            new() { Name = prefix + nameof(ProductPart.ExternalButtonText), Description = S["The label of the link to where an external product is sold."] },
            new() { Name = prefix + nameof(ProductPart.MinQuantity), Description = S["The fewest a buyer can buy."] },
            new() { Name = prefix + nameof(ProductPart.MaxQuantity), Description = S["The most a buyer can buy."] },
            new() { Name = prefix + nameof(ProductPart.QuantityStep), Description = S["The pack size a quantity has to be a multiple of."] },
        ];
    }

    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(ProductPart);
        var columns = GetColumns(context);
        var part = context.ContentItem.Get<ProductPart>(name) ?? new ProductPart();

        foreach (DataColumn column in context.Columns)
        {
            var value = context.Row[column]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(value) || columns.FirstOrDefault(entry => Is(column.ColumnName, entry)) is not { } matched)
            {
                continue;
            }

            switch (matched.Name[(name.Length + 1)..])
            {
                case nameof(ProductPart.Price):
                    part.Price = Decimal(value) ?? part.Price;
                    break;
                case nameof(ProductPart.CompareAtPrice):
                    part.CompareAtPrice = Decimal(value) ?? part.CompareAtPrice;
                    break;
                case nameof(ProductPart.SalePrice):
                    part.SalePrice = Decimal(value) ?? part.SalePrice;
                    break;
                case nameof(ProductPart.SaleStartsUtc):
                    part.SaleStartsUtc = Date(value) ?? part.SaleStartsUtc;
                    break;
                case nameof(ProductPart.SaleEndsUtc):
                    part.SaleEndsUtc = Date(value) ?? part.SaleEndsUtc;
                    break;
                case nameof(ProductPart.Sku):
                    part.Sku = value;
                    break;
                case nameof(ProductPart.Currency):
                    part.Currency = value.ToUpperInvariant();
                    break;
                case nameof(ProductPart.ExternalUrl):
                    part.ExternalUrl = Uri.TryCreate(value, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp) ? value : part.ExternalUrl;
                    break;
                case nameof(ProductPart.ExternalButtonText):
                    part.ExternalButtonText = value;
                    break;
                case nameof(ProductPart.MinQuantity):
                    part.MinQuantity = Integer(value) is > 1 and var min ? min : part.MinQuantity;
                    break;
                case nameof(ProductPart.MaxQuantity):
                    part.MaxQuantity = Integer(value) is > 0 and var max ? max : part.MaxQuantity;
                    break;
                case nameof(ProductPart.QuantityStep):
                    part.QuantityStep = Integer(value) is > 1 and var step ? step : part.QuantityStep;
                    break;
            }
        }

        context.ContentItem.Apply(name, part);

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(ProductPart);

        if (context.ContentItem.Get<ProductPart>(name) is not { } part)
        {
            return Task.CompletedTask;
        }

        var prefix = name + "_";

        context.Row[prefix + nameof(ProductPart.Price)] = Money(part.Price);
        context.Row[prefix + nameof(ProductPart.CompareAtPrice)] = Money(part.CompareAtPrice);
        context.Row[prefix + nameof(ProductPart.SalePrice)] = Money(part.SalePrice);
        context.Row[prefix + nameof(ProductPart.SaleStartsUtc)] = part.SaleStartsUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        context.Row[prefix + nameof(ProductPart.SaleEndsUtc)] = part.SaleEndsUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        context.Row[prefix + nameof(ProductPart.Sku)] = part.Sku;
        context.Row[prefix + nameof(ProductPart.Currency)] = part.Currency;
        context.Row[prefix + nameof(ProductPart.ExternalUrl)] = part.ExternalUrl;
        context.Row[prefix + nameof(ProductPart.ExternalButtonText)] = part.ExternalButtonText;
        context.Row[prefix + nameof(ProductPart.MinQuantity)] = part.MinQuantity?.ToString(CultureInfo.InvariantCulture);
        context.Row[prefix + nameof(ProductPart.MaxQuantity)] = part.MaxQuantity?.ToString(CultureInfo.InvariantCulture);
        context.Row[prefix + nameof(ProductPart.QuantityStep)] = part.QuantityStep?.ToString(CultureInfo.InvariantCulture);

        return Task.CompletedTask;
    }

    internal static decimal? Decimal(string value)
        => decimal.TryParse(value?.Trim().TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0 ? amount : null;

    internal static int? Integer(string value)
        => int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    internal static string Money(decimal? amount)
        => amount?.ToString("0.00", CultureInfo.InvariantCulture);

    private static DateTime? Date(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? date : null;
}

using CrestApps.OrchardCore.Products.Core.Listing;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

/// <summary>
/// Maps <see cref="ProductSearchDocument"/> records to the product and attribute index rows.
/// </summary>
public sealed class ProductSearchIndexProvider : IndexProvider<ProductSearchDocument>
{
    /// <summary>
    /// The longest text an index column holds. Longer values are cut, because a filter option or a sort key
    /// never needs more and a wider column cannot be indexed on every database.
    /// </summary>
    public const int MaxTextLength = 255;

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ProductSearchDocument> context)
    {
        context.For<ProductSearchIndex>()
            .Map(document => new ProductSearchIndex
            {
                ContentItemId = document.ContentItemId,
                ContentType = document.ContentType,
                Culture = document.Culture,
                Title = Truncate(document.Title),
                Sku = Truncate(document.Sku),
                Price = document.Price,
                IsPurchasable = document.IsPurchasable,
                Featured = document.Featured,
                Position = document.Position,
                PublishedUtc = document.PublishedUtc,
            });

        context.For<ProductSaleIndex>()
            .When(document => document.SaleStartsUtc.HasValue || document.SaleEndsUtc.HasValue)
            .Map(document => new ProductSaleIndex
            {
                ContentItemId = document.ContentItemId,
                StartsUtc = document.SaleStartsUtc,
                EndsUtc = document.SaleEndsUtc,
            });

        context.For<ProductAttributeIndex>()
            .Map(document => (document.Attributes ?? [])
                .Where(value => !string.IsNullOrEmpty(value.Key))
                .Select(value => new ProductAttributeIndex
                {
                    ContentItemId = document.ContentItemId,
                    ContentType = document.ContentType,
                    AttributeKey = Truncate(value.Key),
                    TextValue = Truncate(value.Text),
                    NumberValue = value.Number,
                    DateValue = value.Date,
                    Label = Truncate(value.Label),
                    Level = value.Level,
                    SortOrder = value.Order,
                    Searchable = value.Searchable,
                }));
    }

    private static string Truncate(string value)
        => value is { Length: > MaxTextLength } ? value[..MaxTextLength] : value;
}

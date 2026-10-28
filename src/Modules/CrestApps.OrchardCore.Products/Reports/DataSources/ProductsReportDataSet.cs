using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One row per product: its type, SKU, kind, prices and sale, currency, tax category, categories, whether it is a
/// bundle or sold in variants, and its dates. Prices are in the product's own currency, which the <c>Currency</c> field
/// names.
/// </summary>
public sealed class ProductsReportDataSet : ProductContentReportDataSet<ContentItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        IStringLocalizer<ProductsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ProductSource.Products,
                stringLocalizer["Products"],
                stringLocalizer["One row per product: its type, SKU, kind, prices in its own currency, sale, tax category, categories and dates."]),
            session,
            authorizationService,
            access,
            CommerceReportDataSets.ContentItemIdField)
    {
        var S = stringLocalizer;
        var product = S["Product"].Value;
        var price = S["Price"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the product's currency."].Value;

        AddField(CommerceReportDataSets.ContentItemIdField, S["Product ID"], DataFieldType.Text, item => item.ContentItemId, product).Identifier();
        AddField(nameof(ContentItem.ContentItemVersionId), S["Version ID"], DataFieldType.Text, item => item.ContentItemVersionId, product);
        AddField(ContentTypeField, S["Product type"], DataFieldType.Text, item => item.ContentType, product);
        AddField("Title", S["Product"], DataFieldType.Text, item => item.DisplayText, product);
        AddField(nameof(ProductPart.Sku), S["SKU"], DataFieldType.Text, item => ProductReportValues.Part<ProductPart>(item)?.Sku, product);
        AddField("ProductType", S["Kind of product"], DataFieldType.Text, item => Types.GetSettings(item.ContentType)?.Type ?? ProductType.Undefined, product).Description = S["Good, Service or Digital, as the product's content type sets it."].Value;
        AddField(nameof(ProductPart.IsPurchasable), S["For sale"], DataFieldType.Boolean, item => ProductReportValues.Part<ProductPart>(item)?.CanBePurchased(Types.GetSettings(item.ContentType)) == true, product).Description = S["Whether the product can be bought online, rather than only listed or bought elsewhere."].Value;
        AddField(nameof(ProductPart.Featured), S["Featured"], DataFieldType.Boolean, item => ProductReportValues.Part<ProductPart>(item)?.Featured == true, product);
        AddField("IsBundle", S["Bundle"], DataFieldType.Boolean, item => ProductReportValues.Part<ProductBundlePart>(item)?.Items?.Count > 0, product);
        AddField("VariantCount", S["Variants"], DataFieldType.Integer, item => ProductReportValues.Part<ProductVariantsPart>(item)?.Variants?.Count ?? 0, product);
        AddField("Categories", S["Category IDs"], DataFieldType.Text, ProductReportValues.CategoryIds, product).Description = S["The taxonomy terms the product's taxonomy fields hold, separated by commas."].Value;
        AddField(nameof(TaxationPart.TaxCategoryCode), S["Tax category"], DataFieldType.Text, item => ProductReportValues.Part<TaxationPart>(item)?.TaxCategoryCode, product);
        AddField(nameof(ContentItem.Published), S["Published"], DataFieldType.Boolean, item => item.Published, product).Description = S["Whether this is the published version. A draft is read for the product types the principal may edit."].Value;
        AddField(nameof(ContentItem.Owner), S["Owner ID"], DataFieldType.Text, item => item.Owner, product).Identifier(ReportDataReferences.User());

        AddField(nameof(ProductPart.Currency), S["Currency"], DataFieldType.Text, Currency, price);
        AddField(nameof(ProductPart.Price), S["Price"], DataFieldType.Decimal, item => ProductReportValues.Part<ProductPart>(item)?.Price, price).Description = S["In the product's currency. Empty for a product sold only at its named prices, or whose price is not shown."].Value;
        AddField(nameof(ProductPart.CompareAtPrice), S["Compare-at price"], DataFieldType.Decimal, item => ProductReportValues.Part<ProductPart>(item)?.CompareAtPrice, price).Description = inCurrency;
        AddField(nameof(ProductPart.SalePrice), S["Sale price"], DataFieldType.Decimal, item => ProductReportValues.Part<ProductPart>(item)?.SalePrice, price).Description = S["What the product sells for during its sale, in the product's currency."].Value;
        AddField(nameof(ProductPart.SaleStartsUtc), S["Sale starts"], DataFieldType.DateTime, item => ProductReportValues.Part<ProductPart>(item)?.SaleStartsUtc, price);
        AddField(nameof(ProductPart.SaleEndsUtc), S["Sale ends"], DataFieldType.DateTime, item => ProductReportValues.Part<ProductPart>(item)?.SaleEndsUtc, price);
        AddField("PriceCount", S["Named prices"], DataFieldType.Integer, item => ProductReportValues.Part<ProductPricePart>(item)?.Prices?.Count ?? 0, price).Description = S["The number of named prices the product is offered at, such as a monthly and an annual plan."].Value;

        AddField(nameof(ContentItem.CreatedUtc), S["Created"], DataFieldType.DateTime, item => item.CreatedUtc, dates);
        AddField(nameof(ContentItem.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, item => item.ModifiedUtc, dates);
        AddField(nameof(ContentItem.PublishedUtc), S["Published on"], DataFieldType.DateTime, item => item.PublishedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<ContentItem> GetRows(ContentItem document)
    {
        yield return document;
    }
}

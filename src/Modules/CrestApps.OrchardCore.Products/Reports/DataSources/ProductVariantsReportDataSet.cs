using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One row per variant of a product sold in variants: its options, the SKU and prices it is sold at, falling back to
/// the product's where it has none of its own, and whether buyers can choose it. Prices are in the product's currency.
/// The variant's image is never exposed.
/// </summary>
public sealed class ProductVariantsReportDataSet : ProductEntryReportDataSet<ProductVariant>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductVariantsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductVariantsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        IStringLocalizer<ProductVariantsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ProductSource.Variants,
                stringLocalizer["Product variants"],
                stringLocalizer["One row per variant of a product sold in variants: its options, SKU, prices in the product's currency, and whether buyers can choose it."]),
            session,
            authorizationService,
            access,
            ProductIdField)
    {
        var S = stringLocalizer;
        var product = S["Product"].Value;
        var variant = S["Variant"].Value;
        var inCurrency = S["In the product's currency."].Value;

        AddProductFields(ProductIdField, S["Product ID"], "ProductTitle", S["Product"], S["Product type"], S["Published"], product);
        AddField(nameof(ProductPart.Currency), S["Currency"], DataFieldType.Text, row => Currency(row.Parent), product);

        AddField(CommerceReportDataSets.VariantIdField, S["Variant ID"], DataFieldType.Text, row => row.Child.VariantId, variant).Identifier();
        AddField("Position", S["Variant number"], DataFieldType.Integer, row => row.Position, variant);
        AddField("VariantTitle", S["Variant"], DataFieldType.Text, row => row.Child.GetTitle(OptionNames(row.Parent)), variant).Description = S["The variant's option values in option order, such as Red / M."].Value;
        AddField(nameof(ProductVariant.Sku), S["SKU"], DataFieldType.Text, row => string.IsNullOrEmpty(row.Child.Sku) ? ProductReportValues.Part<ProductPart>(row.Parent)?.Sku : row.Child.Sku, variant).Description = S["The variant's own SKU, else the product's."].Value;
        AddField(nameof(ProductVariant.Price), S["Price"], DataFieldType.Decimal, row => row.Child.Price ?? ProductReportValues.Part<ProductPart>(row.Parent)?.Price, variant).Description = S["The variant's own price, else the product's, in the product's currency."].Value;
        AddField("HasOwnPrice", S["Own price"], DataFieldType.Boolean, row => row.Child.Price.HasValue, variant);
        AddField(nameof(ProductVariant.CompareAtPrice), S["Compare-at price"], DataFieldType.Decimal, row => row.Child.CompareAtPrice ?? ProductReportValues.Part<ProductPart>(row.Parent)?.CompareAtPrice, variant).Description = inCurrency;
        AddField(nameof(ProductVariant.SalePrice), S["Sale price"], DataFieldType.Decimal, row => SalePrice(row.Parent, row.Child), variant).Description = S["What the variant sells for during the product's sale, in the product's currency."].Value;
        AddField(nameof(ProductVariant.Enabled), S["Can be chosen"], DataFieldType.Boolean, row => row.Child.Enabled, variant);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<ProductVariant> GetEntries(ContentItem product)
    {
        return ProductReportValues.Part<ProductVariantsPart>(product)?.Variants;
    }

    private static IEnumerable<string> OptionNames(ContentItem product)
    {
        return ProductReportValues.Part<ProductVariantsPart>(product)?.Options?.Select(option => option.Name);
    }

    // A variant priced on its own isn't put on sale by the product's sale price: that price was for the product's.
    private static decimal? SalePrice(ContentItem product, ProductVariant variant)
    {
        return variant.SalePrice ?? (variant.Price is null ? ProductReportValues.Part<ProductPart>(product)?.SalePrice : null);
    }
}

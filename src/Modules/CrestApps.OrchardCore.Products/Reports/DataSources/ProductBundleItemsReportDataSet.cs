using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One row per product a bundle or kit is made of, and how many of it each bundle holds, so a report can show which
/// bundles a product is part of.
/// </summary>
public sealed class ProductBundleItemsReportDataSet : ProductEntryReportDataSet<ProductBundleItem>
{
    /// <summary>
    /// The name of the field that holds the identifier of the bundle.
    /// </summary>
    public const string BundleIdField = "BundleId";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductBundleItemsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductBundleItemsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        IStringLocalizer<ProductBundleItemsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ProductSource.BundleItems,
                stringLocalizer["Product bundle items"],
                stringLocalizer["One row per product a bundle is made of, and how many of it each bundle holds."]),
            session,
            authorizationService,
            access,
            BundleIdField)
    {
        var S = stringLocalizer;
        var bundle = S["Bundle"].Value;
        var item = S["Item"].Value;

        AddProductFields(BundleIdField, S["Bundle ID"], "BundleTitle", S["Bundle"], S["Bundle type"], S["Published"], bundle);

        AddField(ProductIdField, S["Product ID"], DataFieldType.Text, row => row.Child.ContentItemId, item).Identifier(CommerceReportDataSets.ProductReference());
        AddField("ProductTitle", S["Product"], DataFieldType.Text, row => row.Child.Title, item).Description = S["The product's title when the bundle was saved."].Value;
        AddField(nameof(ProductBundleItem.Quantity), S["Quantity"], DataFieldType.Integer, row => row.Child.Quantity, item).Description = S["How many of the product each bundle holds."].Value;
        AddField("Position", S["Item number"], DataFieldType.Integer, row => row.Position, item);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<ProductBundleItem> GetEntries(ContentItem product)
    {
        return ProductReportValues.Part<ProductBundlePart>(product)?.Items;
    }
}

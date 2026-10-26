using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per product line of an order: the product or variant bought, how many, and at what price, as it was when the
/// order was placed.
/// </summary>
public sealed class OrderLinesReportDataSet : OrderEntryReportDataSet<OrderLine>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderLinesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrderLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.OrderLines,
                stringLocalizer["Order lines"],
                stringLocalizer["One row per product line of an order: the product or variant, quantity and prices in the order's currency, as they were when it was placed."]),
            session,
            authorizationService,
            stringLocalizer)
    {
        var S = stringLocalizer;
        var line = S["Line"].Value;
        var product = S["Product"].Value;
        var inCurrency = S["In the order's currency."].Value;

        AddField(nameof(OrderLine.LineId), S["Line ID"], DataFieldType.Text, row => row.Child.LineId, line).Identifier();
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField(nameof(OrderLine.Quantity), S["Quantity"], DataFieldType.Integer, row => row.Child.Quantity, line);
        AddField(nameof(OrderLine.UnitPrice), S["Unit price"], DataFieldType.Decimal, row => row.Child.UnitPrice, line).Description = inCurrency;
        AddField(nameof(OrderLine.LineTotal), S["Line total"], DataFieldType.Decimal, row => row.Child.LineTotal, line).Description = inCurrency;
        AddField(nameof(OrderLine.QuotedUnitPrice), S["Quoted unit price"], DataFieldType.Decimal, row => row.Child.QuotedUnitPrice, line).Description = inCurrency;
        AddField(nameof(OrderLine.PriceName), S["Price"], DataFieldType.Text, row => row.Child.PriceName, line);
        AddField("AddOns", S["Add-ons"], DataFieldType.Text, row => Join(row.Child.AddOns?.Select(addOn => addOn.Label)), line);
        AddField("AddOnAmount", S["Add-ons per unit"], DataFieldType.Decimal, row => row.Child.AddOns?.Sum(addOn => addOn.Price) ?? 0m, line).Description = inCurrency;
        AddField(nameof(OrderLine.ExcludeFromDiscounts), S["Excluded from discounts"], DataFieldType.Boolean, row => row.Child.ExcludeFromDiscounts, line);
        AddField(nameof(OrderLine.ShipsFromUtc), S["Ships from"], DataFieldType.DateTime, row => row.Child.ShipsFromUtc, line);

        AddField("ProductId", S["Product ID"], DataFieldType.Text, row => row.Child.ContentItemId, product)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField("ProductVersionId", S["Product version ID"], DataFieldType.Text, row => row.Child.ContentItemVersionId, product);
        AddField(nameof(OrderLine.ContentType), S["Product type"], DataFieldType.Text, row => row.Child.ContentType, product);
        AddField(nameof(OrderLine.Title), S["Product"], DataFieldType.Text, row => row.Child.Title, product);
        AddField(nameof(OrderLine.Sku), S["SKU"], DataFieldType.Text, row => row.Child.Sku, product);
        AddField(nameof(OrderLine.VariantId), S["Variant ID"], DataFieldType.Text, row => row.Child.VariantId, product)
            .Identifier(CommerceReportDataSets.VariantReference());
        AddField(nameof(OrderLine.VariantTitle), S["Variant"], DataFieldType.Text, row => row.Child.VariantTitle, product);
        AddField(nameof(OrderLine.ProductType), S["Kind of product"], DataFieldType.Text, row => row.Child.ProductType, product);
        AddField("Categories", S["Category IDs"], DataFieldType.Text, row => Join(row.Child.CategoryIds), product);
        AddField("IsBundle", S["Bundle"], DataFieldType.Boolean, row => row.Child.BundleItems?.Count > 0, product);
        AddField(nameof(OrderLine.TaxCategoryCode), S["Tax category"], DataFieldType.Text, row => row.Child.TaxCategoryCode, product);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<OrderLine> GetEntries(Order order)
    {
        return order.Lines;
    }

    private static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }
}

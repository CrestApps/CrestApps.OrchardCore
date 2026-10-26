using System.Globalization;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.Reports.DataSources;

/// <summary>
/// One row per request a shopper made to be emailed when a product, or one of its variants, is back in stock: what it
/// is for, when it was made, and whether the email went out. Like the item's admin page, which shows only how many
/// shoppers wait, it never exposes who asked: neither the email address nor the user.
/// </summary>
public sealed class StockAlertsReportDataSet : IndexedReportDataSet<StockAlert, StockAlertIndex>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the shopper asked, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="StockAlertsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session back-in-stock alerts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StockAlertsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StockAlertsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.StockAlerts,
                stringLocalizer["Back-in-stock alerts"],
                stringLocalizer["One row per request to be emailed when a product is back in stock: the item, when it was made and whether the email went out."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var alert = S["Alert"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Alert ID"], DataFieldType.Text, record => record.Id.ToString(CultureInfo.InvariantCulture), alert).Identifier();
        AddField(nameof(StockAlert.StockItemId), S["Stock item ID"], DataFieldType.Text, record => record.StockItemId, alert)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.StockItems));
        AddField(StockItemsReportDataSet.ProductIdField, S["Product ID"], DataFieldType.Text, record => record.ContentItemId, alert)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(StockAlert.VariantId), S["Variant ID"], DataFieldType.Text, record => record.VariantId, alert)
            .Identifier(CommerceReportDataSets.VariantReference());
        AddField(nameof(StockAlertIndex.Waiting), S["Waiting"], DataFieldType.Boolean, record => record.SentUtc is null, alert).Description = S["The email has not gone out yet."].Value;
        AddField(CreatedUtcField, S["Asked"], DataFieldType.DateTime, record => record.CreatedUtc, alert);
        AddField(nameof(StockAlert.SentUtc), S["Emailed"], DataFieldType.DateTime, record => record.SentUtc, alert);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<StockAlertIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StockAlertIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StockAlertIndex, string>>>(StringComparer.Ordinal)
        {
            [nameof(StockAlert.StockItemId)] = index => index.StockItemId,
            [StockItemsReportDataSet.ProductIdField] = index => index.ContentItemId,
            [nameof(StockAlert.VariantId)] = index => index.VariantId,
        };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the product in its ContentItemId column.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [StockItemsReportDataSet.ProductIdField] = nameof(StockAlertIndex.ContentItemId),
        };

        return columns;
    }
}

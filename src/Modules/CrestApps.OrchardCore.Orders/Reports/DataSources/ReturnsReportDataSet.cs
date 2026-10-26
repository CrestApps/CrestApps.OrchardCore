using System.Linq.Expressions;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per return a buyer asked for: the order it is for, what is sent back, how it is resolved, and where it
/// stands.
/// </summary>
public sealed class ReturnsReportDataSet : IndexedReportDataSet<OrderReturn, OrderReturnIndex>, IOrderReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the return was asked for, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session returns are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReturnsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReturnsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.Returns,
                stringLocalizer["Returns"],
                stringLocalizer["One row per return a buyer asked for: its order, units, resolution, refund in the order's currency and status."]),
            session,
            authorizationService,
            OrderConstants.CollectionName,
            OrderPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var orderReturn = S["Return"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Return ID"], DataFieldType.Text, record => record.ItemId, orderReturn).Identifier();
        AddField(nameof(OrderReturn.Number), S["Return number"], DataFieldType.Text, record => record.Number, orderReturn);
        AddField(nameof(OrderReturn.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, orderReturn)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(OrderReturn.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, orderReturn);
        AddField(nameof(OrderReturn.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, orderReturn).Identifier(ReportDataReferences.User());
        AddField(nameof(OrderReturn.Status), S["Status"], DataFieldType.Text, record => record.Status, orderReturn);
        AddField(nameof(OrderReturn.Resolution), S["Resolution"], DataFieldType.Text, record => record.Resolution, orderReturn);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, orderReturn);
        AddField(nameof(OrderReturn.Quantity), S["Units"], DataFieldType.Integer, record => record.Lines?.Sum(line => line.Quantity) ?? 0, orderReturn);
        AddField("RestockedUnits", S["Units restocked"], DataFieldType.Integer, record => record.Lines?.Where(line => line.Restocked).Sum(line => line.Quantity) ?? 0, orderReturn);
        AddField(nameof(OrderReturn.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, orderReturn);
        AddField("ReturnedAmount", S["Value returned"], DataFieldType.Decimal, record => record.Lines?.Sum(line => line.UnitPrice * line.Quantity) ?? 0m, orderReturn).Description = S["The units sent back at the prices paid, in the order's currency."].Value;
        AddField(nameof(OrderReturn.RefundedAmount), S["Refunded"], DataFieldType.Decimal, record => record.RefundedAmount, orderReturn).Description = S["In the order's currency."].Value;
        AddField(nameof(OrderReturn.RestockLocationId), S["Restock location ID"], DataFieldType.Text, record => record.RestockLocationId, orderReturn)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InventorySource.Name, CommerceReportDataSets.InventorySource.Locations));

        AddField(CreatedUtcField, S["Asked for"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(OrderReturn.ApprovedUtc), S["Approved"], DataFieldType.DateTime, record => record.ApprovedUtc, dates);
        AddField(nameof(OrderReturn.ReceivedUtc), S["Received"], DataFieldType.DateTime, record => record.ReceivedUtc, dates);
        AddField(nameof(OrderReturn.ClosedUtc), S["Closed"], DataFieldType.DateTime, record => record.ClosedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<OrderReturnIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<OrderReturnIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<OrderReturnIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(OrderReturn.OrderId)] = index => index.OrderId,
            [nameof(OrderReturn.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<OrderReturnIndex> columns)
    {
        columns.Enum(nameof(OrderReturn.Status), index => index.Status);
    }
}

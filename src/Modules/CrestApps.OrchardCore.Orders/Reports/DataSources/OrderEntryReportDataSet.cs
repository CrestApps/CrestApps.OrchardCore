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
/// A data set with one row per entry an order holds, such as its lines or its refunds. Orders are read through their
/// index, newest first by when they were placed, and each row carries the order's number, status, sales channel,
/// dates, currency and customer, so a report can group the entries by them without joining the orders.
/// </summary>
/// <typeparam name="TEntry">The type of the entries.</typeparam>
public abstract class OrderEntryReportDataSet<TEntry> : IndexedReportDataSet<Order, OrderIndex, ReportChildRow<Order, TEntry>>, IOrderReportDataSet
    where TEntry : class
{
    /// <summary>
    /// The name of the field that holds the identifier of the order an entry belongs to.
    /// </summary>
    public const string OrderIdField = "OrderId";

    /// <summary>
    /// The name of the field that holds when the order was placed, the data set's main date.
    /// </summary>
    public const string OrderCreatedUtcField = "OrderCreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderEntryReportDataSet{TEntry}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected OrderEntryReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer stringLocalizer)
        : base(descriptor, session, authorizationService, OrderConstants.CollectionName, OrderPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var order = S["Order"].Value;

        AddField(OrderIdField, S["Order ID"], DataFieldType.Text, row => row.Parent.ItemId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField("OrderNumber", S["Order number"], DataFieldType.Text, row => row.Parent.OrderNumber, order);
        AddField("OrderStatus", S["Order status"], DataFieldType.Text, row => row.Parent.Status, order);
        AddField("OrderChannel", S["Sales channel"], DataFieldType.Text, row => row.Parent.Channel, order);
        AddField(OrderCreatedUtcField, S["Order placed"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, order);
        AddField("OrderPaidUtc", S["Order paid"], DataFieldType.DateTime, row => row.Parent.PaidUtc, order);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Parent.Currency, order);
        AddField("CustomerId", S["Customer ID"], DataFieldType.Text, row => row.Parent.CustomerId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<OrderIndex, DateTime>> Column)? DateColumn
        => (OrderCreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<OrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<OrderIndex, string>>>(StringComparer.Ordinal)
        {
            [OrderIdField] = index => index.ItemId,
            ["CustomerId"] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<OrderIndex> columns)
    {
        columns
            .Enum("OrderStatus", index => index.Status)
            .Text("OrderChannel", nameof(OrderIndex.Channel))
            .Text("OrderNumber", nameof(OrderIndex.OrderNumber));
    }

    /// <summary>
    /// Gets the entries an order holds.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The entries, or <see langword="null"/> when it holds none.</returns>
    protected abstract IEnumerable<TEntry> GetEntries(Order order);

    /// <inheritdoc/>
    protected sealed override IEnumerable<ReportChildRow<Order, TEntry>> GetRows(Order document)
    {
        return ReportChildRow<Order, TEntry>.For(document, GetEntries(document));
    }
}

using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per refund that went through for an order: how much was given back, when, and why.
/// </summary>
public sealed class OrderRefundsReportDataSet : OrderEntryReportDataSet<OrderRefund>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRefundsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderRefundsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrderRefundsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.OrderRefunds,
                stringLocalizer["Order refunds"],
                stringLocalizer["One row per refund given for an order: the amount in the order's currency, when, and why."]),
            session,
            authorizationService,
            stringLocalizer)
    {
        var S = stringLocalizer;
        var refund = S["Refund"].Value;

        AddField(nameof(OrderRefund.RefundId), S["Refund ID"], DataFieldType.Text, row => row.Child.RefundId, refund).Identifier();
        AddField(nameof(OrderRefund.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, refund).Description = S["In the order's currency."].Value;
        AddField(nameof(OrderRefund.Reason), S["Reason"], DataFieldType.Text, row => row.Child.Reason, refund);
        AddField("RefundedUtc", S["Refunded"], DataFieldType.DateTime, row => row.Child.CreatedUtc, refund);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<OrderRefund> GetEntries(Order order)
    {
        return order.Refunds;
    }
}

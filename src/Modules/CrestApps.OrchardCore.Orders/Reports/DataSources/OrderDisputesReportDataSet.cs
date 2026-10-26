using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per dispute, or chargeback, a payer opened against an order's payment: the amount, the reason the gateway
/// gave, and where it stands.
/// </summary>
public sealed class OrderDisputesReportDataSet : OrderEntryReportDataSet<OrderDispute>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderDisputesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderDisputesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<OrderDisputesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.OrderDisputes,
                stringLocalizer["Order disputes"],
                stringLocalizer["One row per dispute, or chargeback, opened against an order's payment: its amount, reason and status."]),
            session,
            authorizationService,
            stringLocalizer)
    {
        var S = stringLocalizer;
        var dispute = S["Dispute"].Value;

        AddField("DisputeReference", S["Dispute reference"], DataFieldType.Text, row => row.Child.Reference, dispute).Identifier();
        AddField(nameof(OrderDispute.Gateway), S["Gateway"], DataFieldType.Text, row => row.Child.Gateway, dispute);
        AddField(nameof(OrderDispute.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, dispute).Description = S["In the dispute's currency."].Value;
        AddField("DisputeCurrency", S["Dispute currency"], DataFieldType.Text, row => row.Child.Currency ?? row.Parent.Currency, dispute);
        AddField(nameof(OrderDispute.Reason), S["Reason"], DataFieldType.Text, row => row.Child.Reason, dispute);
        AddField(nameof(OrderDispute.Status), S["Status"], DataFieldType.Text, row => row.Child.Status, dispute);
        AddField(nameof(OrderDispute.IsClosed), S["Closed"], DataFieldType.Boolean, row => row.Child.IsClosed, dispute);
        AddField("OpenedUtc", S["Opened"], DataFieldType.DateTime, row => row.Child.CreatedUtc, dispute);
        AddField(nameof(OrderDispute.UpdatedUtc), S["Last updated"], DataFieldType.DateTime, row => row.Child.UpdatedUtc, dispute);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IEnumerable<OrderDispute> GetEntries(Order order)
    {
        return order.Disputes;
    }
}

using System.Linq.Expressions;
using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Marketplace.Reports.DataSources;

/// <summary>
/// One row per entry of a vendor's earnings: what a sale owes the vendor after the store's commission, or what a refund
/// takes back, and where it stands, from held in escrow to paid out. Money is in the order's currency, which the
/// <c>Currency</c> field names.
/// </summary>
public sealed class VendorEarningsReportDataSet : IndexedReportDataSet<VendorEarning, VendorEarningIndex>, IMarketplaceReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the earning was recorded, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="VendorEarningsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session earnings are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VendorEarningsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<VendorEarningsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.MarketplaceSource.Earnings,
                stringLocalizer["Vendor earnings"],
                stringLocalizer["One row per sale or refund of a vendor: the gross, the store's commission and the vendor's net in the order's currency, and whether it is held, released or paid out."]),
            session,
            authorizationService,
            MarketplaceConstants.CollectionName,
            MarketplacePermissions.ManageMarketplace)
    {
        var S = stringLocalizer;
        var earning = S["Earning"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the order's currency; negative for a refund."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Earning ID"], DataFieldType.Text, record => record.ItemId, earning).Identifier();
        AddField(nameof(VendorEarning.VendorId), S["Vendor ID"], DataFieldType.Text, record => record.VendorId, earning)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.MarketplaceSource.Name, CommerceReportDataSets.MarketplaceSource.Vendors));
        AddField(nameof(VendorEarning.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, earning)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(VendorEarning.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, earning);
        AddField(nameof(VendorEarning.LineId), S["Order line ID"], DataFieldType.Text, record => record.LineId, earning)
            .Identifier(new DataFieldReference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.OrderLines, nameof(OrderLine.LineId)));
        AddField(nameof(VendorEarning.Title), S["Item"], DataFieldType.Text, record => record.Title, earning);
        AddField(nameof(VendorEarning.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, earning).Description = S["Sale or Refund."].Value;
        AddField(nameof(VendorEarning.Status), S["Status"], DataFieldType.Text, record => record.Status, earning).Description = S["Held in escrow, Available to pay out, or PaidOut."].Value;
        AddField(nameof(VendorEarning.AwaitsFulfillment), S["Waits for fulfillment"], DataFieldType.Boolean, record => record.AwaitsFulfillment, earning);
        AddField(nameof(VendorEarning.PayoutId), S["Payout ID"], DataFieldType.Text, record => record.PayoutId, earning)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.MarketplaceSource.Name, CommerceReportDataSets.MarketplaceSource.Payouts));

        AddField(nameof(VendorEarning.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(VendorEarning.Gross), S["Gross"], DataFieldType.Decimal, record => record.Gross, money).Description = S["What the buyer paid for the line after its share of the order's discounts, in the order's currency; negative for a refund."].Value;
        AddField(nameof(VendorEarning.CommissionPercent), S["Commission (%)"], DataFieldType.Decimal, record => record.CommissionPercent, money);
        AddField(nameof(VendorEarning.Commission), S["Commission"], DataFieldType.Decimal, record => record.Commission, money).Description = inCurrency;
        AddField(nameof(VendorEarning.Net), S["Net"], DataFieldType.Decimal, record => record.Net, money).Description = S["What the vendor gets, the gross less the commission, in the order's currency; negative for a refund."].Value;

        AddField(CreatedUtcField, S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(VendorEarning.HoldUntilUtc), S["Held until"], DataFieldType.DateTime, record => record.HoldUntilUtc, dates);
        AddField(nameof(VendorEarning.ReleasedUtc), S["Released"], DataFieldType.DateTime, record => record.ReleasedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<VendorEarningIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<VendorEarningIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<VendorEarningIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(VendorEarning.VendorId)] = index => index.VendorId,
            [nameof(VendorEarning.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<VendorEarningIndex> columns)
    {
        columns.Enum(nameof(VendorEarning.Status), index => index.Status);
    }
}

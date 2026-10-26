using System.Linq.Expressions;
using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Marketplace.Reports.DataSources;

/// <summary>
/// One row per transfer of a vendor's released earnings: how much, in which currency, for which order, and whether it
/// was sent. The payment provider's reference for the transfer and its error messages are never exposed.
/// </summary>
public sealed class VendorPayoutsReportDataSet : IndexedReportDataSet<VendorPayout, VendorPayoutIndex>, IMarketplaceReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the payout was made, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="VendorPayoutsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session payouts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VendorPayoutsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<VendorPayoutsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.MarketplaceSource.Payouts,
                stringLocalizer["Vendor payouts"],
                stringLocalizer["One row per transfer of a vendor's released earnings: the amount in its currency, the order it is for, and whether it was sent."]),
            session,
            authorizationService,
            MarketplaceConstants.CollectionName,
            MarketplacePermissions.ManageMarketplace)
    {
        var S = stringLocalizer;
        var payout = S["Payout"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Payout ID"], DataFieldType.Text, record => record.ItemId, payout).Identifier();
        AddField(nameof(VendorPayout.VendorId), S["Vendor ID"], DataFieldType.Text, record => record.VendorId, payout)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.MarketplaceSource.Name, CommerceReportDataSets.MarketplaceSource.Vendors));
        AddField(nameof(VendorPayout.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, payout)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(VendorPayout.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, payout);
        AddField(nameof(VendorPayout.Status), S["Status"], DataFieldType.Text, record => record.Status, payout).Description = S["Paid or Failed."].Value;
        AddField(nameof(VendorPayout.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, payout);
        AddField(nameof(VendorPayout.Amount), S["Amount"], DataFieldType.Decimal, record => record.Amount, payout).Description = S["In the payout's currency."].Value;
        AddField("EarningCount", S["Earnings paid"], DataFieldType.Integer, record => record.EarningIds?.Count ?? 0, payout).Description = S["How many earnings the payout paid."].Value;
        AddField(CreatedUtcField, S["Made"], DataFieldType.DateTime, record => record.CreatedUtc, payout);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<VendorPayoutIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<VendorPayoutIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<VendorPayoutIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(VendorPayout.VendorId)] = index => index.VendorId,
        };
}

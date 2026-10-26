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
/// One row per seller of the marketplace: its status, the commission the store keeps on its sales, whether it can be
/// paid out, and the people who run it. Its payout account and the keys used to make it are never exposed.
/// </summary>
public sealed class VendorsReportDataSet : IndexedReportDataSet<Vendor, VendorIndex>, IMarketplaceReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VendorsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session vendors are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VendorsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<VendorsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.MarketplaceSource.Vendors,
                stringLocalizer["Vendors"],
                stringLocalizer["One row per seller of the marketplace: its status, commission, whether it can be paid out, and the people who run it."]),
            session,
            authorizationService,
            MarketplaceConstants.CollectionName,
            MarketplacePermissions.ManageMarketplace)
    {
        var S = stringLocalizer;
        var vendor = S["Vendor"].Value;
        var payouts = S["Payouts"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Vendor ID"], DataFieldType.Text, record => record.ItemId, vendor).Identifier();
        AddField(nameof(Vendor.Name), S["Vendor"], DataFieldType.Text, record => record.Name, vendor);
        AddField(nameof(Vendor.Email), S["Email"], DataFieldType.Text, record => record.Email, vendor);
        AddField(nameof(Vendor.Status), S["Status"], DataFieldType.Text, record => record.Status, vendor);
        AddField(nameof(Vendor.CommissionPercent), S["Commission (%)"], DataFieldType.Decimal, record => record.CommissionPercent, vendor).Description = S["The percent the store keeps of each sale; empty when the vendor has the store's default."].Value;
        AddField("UserCount", S["People"], DataFieldType.Integer, record => record.UserIds?.Count ?? 0, vendor).Description = S["How many users run the vendor."].Value;
        AddField(nameof(Vendor.UserNames), S["Run by"], DataFieldType.Text, record => Join(record.UserNames), vendor).Description = S["The usernames of the people who run the vendor, as they were when added."].Value;
        AddField(nameof(Vendor.CreatedUtc), S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, vendor);
        AddField(nameof(Vendor.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, vendor);

        AddField(nameof(Vendor.Country), S["Country"], DataFieldType.Text, record => record.Country, payouts).Description = S["The country its payout account is made in."].Value;
        AddField("HasPayoutAccount", S["Has a payout account"], DataFieldType.Boolean, record => !string.IsNullOrEmpty(record.StripeAccountId), payouts);
        AddField(nameof(Vendor.PayoutsEnabled), S["Payouts enabled"], DataFieldType.Boolean, record => record.PayoutsEnabled, payouts).Description = S["Whether its payout account is set up and can take transfers."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<VendorIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<VendorIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<VendorIndex> columns)
    {
        columns.Enum(nameof(Vendor.Status), index => index.Status);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps at most the first 255 characters of the name, so it is not always the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Vendor.Name));

        return columns;
    }

    private static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }
}

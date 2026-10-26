using System.Linq.Expressions;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Companies.Reports.DataSources;

/// <summary>
/// One row per price list companies buy on: the percentage off every product it does not price by SKU, and how many
/// SKUs it prices, in the currency the <c>Currency</c> field names. Price lists are read newest first.
/// </summary>
public sealed class PriceListsReportDataSet : IndexedReportDataSet<PriceList, PriceListIndex>, ICompanyReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PriceListsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session price lists are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PriceListsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PriceListsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CompanySource.PriceLists,
                stringLocalizer["Price lists"],
                stringLocalizer["One row per price list companies buy on: its percentage off, how many SKUs it prices, and the currency of those prices."]),
            session,
            authorizationService,
            CompaniesConstants.CollectionName,
            CompaniesPermissions.ManageCompanies)
    {
        var S = stringLocalizer;
        var list = S["Price list"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Price list ID"], DataFieldType.Text, record => record.ItemId, list).Identifier();
        AddField(nameof(PriceList.Name), S["Name"], DataFieldType.Text, record => record.Name, list);
        AddField(nameof(PriceList.PercentOff), S["Percent off"], DataFieldType.Decimal, record => record.PercentOff, list).Description = S["The percentage off every product the list does not price by SKU, from 0 to 100. Empty when there is none."].Value;
        AddField(nameof(PriceList.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, list).Description = S["The currency of the list's prices by SKU."].Value;
        AddField("EntryCount", S["Prices by SKU"], DataFieldType.Integer, record => record.Entries?.Count(entry => entry is not null) ?? 0, list);
        AddField(nameof(PriceList.CreatedUtc), S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, list);
        AddField(nameof(PriceList.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, list);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PriceListIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PriceListIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PriceListIndex> columns)
    {
        columns.Text(nameof(PriceList.Name), nameof(PriceListIndex.Name));
    }
}

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
/// One row per price a price list sets for a SKU, in the list's currency, which the <c>Currency</c> field names. Price
/// lists are read newest first, and each list gives its prices in their order.
/// </summary>
public sealed class PriceListEntriesReportDataSet : IndexedReportDataSet<PriceList, PriceListIndex, ReportChildRow<PriceList, PriceListEntry>>, ICompanyReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the price list a price belongs to.
    /// </summary>
    public const string PriceListIdField = "PriceListId";

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceListEntriesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session price lists are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PriceListEntriesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PriceListEntriesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CompanySource.PriceListEntries,
                stringLocalizer["Price list entries"],
                stringLocalizer["One row per price a price list sets for a SKU: the list, the SKU, and the unit price in the list's currency."]),
            session,
            authorizationService,
            CompaniesConstants.CollectionName,
            CompaniesPermissions.ManageCompanies)
    {
        var S = stringLocalizer;
        var list = S["Price list"].Value;
        var entry = S["Price"].Value;

        AddField(PriceListIdField, S["Price list ID"], DataFieldType.Text, row => row.Parent.ItemId, list)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CompanySource.Name, CommerceReportDataSets.CompanySource.PriceLists));
        AddField("PriceListName", S["Price list"], DataFieldType.Text, row => row.Parent.Name, list);
        AddField(nameof(PriceList.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, list);

        AddField("Position", S["Price number"], DataFieldType.Integer, row => row.Position, entry);
        AddField(nameof(PriceListEntry.Sku), S["SKU"], DataFieldType.Text, row => row.Child.Sku, entry);
        AddField(nameof(PriceListEntry.UnitPrice), S["Unit price"], DataFieldType.Decimal, row => row.Child.UnitPrice, entry).Description = S["In the price list's currency."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PriceListIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PriceListIndex, string>>>(StringComparer.Ordinal)
        {
            [PriceListIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PriceListIndex> columns)
    {
        columns.Text("PriceListName", nameof(PriceListIndex.Name));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<PriceList, PriceListEntry>> GetRows(PriceList document)
    {
        return ReportChildRow<PriceList, PriceListEntry>.For(document, document.Entries);
    }
}

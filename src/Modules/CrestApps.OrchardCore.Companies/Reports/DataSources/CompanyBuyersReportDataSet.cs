using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Companies.Reports.DataSources;

/// <summary>
/// One row per person who buys for a company: their user, their role, and the most an order they place may come to
/// before a company admin approves it, in the store's currency, which the <c>Currency</c> field names.
/// </summary>
/// <remarks>
/// The buyers are read with their company, through the companies' index, one company at a time and newest first: the
/// index of the buyers holds a row per buyer, so reading through it would read a company once for each of its buyers.
/// A join on the buyer's user is therefore filtered after the read, not in the database.
/// </remarks>
public sealed class CompanyBuyersReportDataSet : IndexedReportDataSet<Company, CompanyIndex, ReportChildRow<Company, CompanyBuyer>>, ICompanyReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the company a buyer buys for.
    /// </summary>
    public const string CompanyIdField = "CompanyId";

    private readonly ISiteService _siteService;
    private string _storeCurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyBuyersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session companies are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service the store's currency is read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CompanyBuyersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IStringLocalizer<CompanyBuyersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CompanySource.CompanyBuyers,
                stringLocalizer["Company buyers"],
                stringLocalizer["One row per person who buys for a company: the company, the user, their role, and their order limit in the store's currency."]),
            session,
            authorizationService,
            CompaniesConstants.CollectionName,
            CompaniesPermissions.ManageCompanies)
    {
        _siteService = siteService;

        var S = stringLocalizer;
        var company = S["Company"].Value;
        var buyer = S["Buyer"].Value;

        AddField(CompanyIdField, S["Company ID"], DataFieldType.Text, row => row.Parent.ItemId, company)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CompanySource.Name, CommerceReportDataSets.CompanySource.Companies));
        AddField("CompanyName", S["Company"], DataFieldType.Text, row => row.Parent.Name, company);
        AddField("CompanyEnabled", S["Company on"], DataFieldType.Boolean, row => row.Parent.IsEnabled, company);

        AddField("Position", S["Buyer number"], DataFieldType.Integer, row => row.Position, buyer);
        AddField(nameof(CompanyBuyer.UserId), S["User ID"], DataFieldType.Text, row => row.Child.UserId, buyer).Identifier(ReportDataReferences.User());
        AddField(nameof(CompanyBuyer.UserName), S["User name"], DataFieldType.Text, row => row.Child.UserName, buyer);
        AddField(nameof(CompanyBuyer.Email), S["Email"], DataFieldType.Text, row => row.Child.Email, buyer);
        AddField(nameof(CompanyBuyer.Role), S["Role"], DataFieldType.Text, row => row.Child.Role, buyer);
        AddField("Currency", S["Currency"], DataFieldType.Text, _ => _storeCurrency, buyer).Description = S["The store's currency, which the order limit is in."].Value;
        AddField(nameof(CompanyBuyer.OrderLimit), S["Order limit"], DataFieldType.Decimal, row => row.Child.OrderLimit, buyer).Description = S["The most an order the buyer places may come to before a company admin approves it, in the store's currency. Empty when there is no limit."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CompanyIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CompanyIndex, string>>>(StringComparer.Ordinal)
        {
            [CompanyIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CompanyIndex> columns)
    {
        columns.Text("CompanyName", nameof(CompanyIndex.Name));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Company, CompanyBuyer>> GetRows(Company document)
    {
        return ReportChildRow<Company, CompanyBuyer>.For(document, document.Buyers);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<ReportChildRow<Company, CompanyBuyer>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>())?.Currency;

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            yield return row;
        }
    }
}

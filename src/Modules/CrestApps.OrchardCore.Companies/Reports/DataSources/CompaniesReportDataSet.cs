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
/// One row per company that buys from the store: its buyers and locations, its payment terms, its price list, and its
/// credit limit and monthly budget. The credit limit and the budget are in the store's currency, as the company's admin
/// page shows them, which the <c>Currency</c> field names. The punchout key is never exposed. Companies are read
/// newest first.
/// </summary>
public sealed class CompaniesReportDataSet : IndexedReportDataSet<Company, CompanyIndex>, ICompanyReportDataSet
{
    private readonly ISiteService _siteService;
    private string _storeCurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompaniesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session companies are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service the store's currency is read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CompaniesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IStringLocalizer<CompaniesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CompanySource.Companies,
                stringLocalizer["Companies"],
                stringLocalizer["One row per company that buys from the store: its buyers, locations, payment terms, price list, and its credit limit and monthly budget in the store's currency."]),
            session,
            authorizationService,
            CompaniesConstants.CollectionName,
            CompaniesPermissions.ManageCompanies)
    {
        _siteService = siteService;

        var S = stringLocalizer;
        var company = S["Company"].Value;
        var terms = S["Payment terms"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inStoreCurrency = S["In the store's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Company ID"], DataFieldType.Text, record => record.ItemId, company).Identifier();
        AddField(nameof(Company.Name), S["Name"], DataFieldType.Text, record => record.Name, company);
        AddField(nameof(Company.TaxId), S["Tax or VAT number"], DataFieldType.Text, record => record.TaxId, company);
        AddField(nameof(Company.BillingEmail), S["Billing email"], DataFieldType.Text, record => record.BillingEmail, company);
        AddField(nameof(Company.IsEnabled), S["On"], DataFieldType.Boolean, record => record.IsEnabled, company);
        AddField("BuyerCount", S["Buyers"], DataFieldType.Integer, record => Buyers(record).Count(), company);
        AddField("AdminCount", S["Admins"], DataFieldType.Integer, record => Buyers(record).Count(buyer => buyer.Role == CompanyBuyerRole.Admin), company);
        AddField("LocationCount", S["Locations"], DataFieldType.Integer, record => record.Locations?.Count(location => location is not null) ?? 0, company);
        AddField(nameof(Company.PriceListId), S["Price list ID"], DataFieldType.Text, record => record.PriceListId, company)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CompanySource.Name, CommerceReportDataSets.CompanySource.PriceLists));
        AddField("PunchoutEnabled", S["Shops by punchout"], DataFieldType.Boolean, record => record.Punchout?.Enabled == true, company);

        AddField("OffersTerms", S["Buys on terms"], DataFieldType.Boolean, record => record.Terms?.OffersTerms == true, terms);
        AddField("NetDays", S["Net days"], DataFieldType.Integer, record => record.Terms?.NetDays, terms);
        AddField("DepositPercent", S["Deposit (%)"], DataFieldType.Decimal, record => record.Terms?.DepositPercent, terms);
        AddField("RequirePurchaseOrder", S["Purchase order required"], DataFieldType.Boolean, record => record.Terms?.RequirePurchaseOrder == true, terms);

        AddField("Currency", S["Currency"], DataFieldType.Text, _ => _storeCurrency, money).Description = S["The store's currency, which the credit limit and the monthly budget are in."].Value;
        AddField("CreditLimit", S["Credit limit"], DataFieldType.Decimal, record => record.Terms?.CreditLimit, money).Description = inStoreCurrency;
        AddField(nameof(Company.MonthlyBudget), S["Monthly budget"], DataFieldType.Decimal, record => record.MonthlyBudget, money).Description = inStoreCurrency;

        AddField(nameof(Company.CreatedUtc), S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Company.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CompanyIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CompanyIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CompanyIndex> columns)
    {
        columns.Text(nameof(Company.Name), nameof(CompanyIndex.Name));
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<Company> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>())?.Currency;

        await foreach (var company in base.LoadAsync(query, cancellationToken))
        {
            yield return company;
        }
    }

    private static IEnumerable<CompanyBuyer> Buyers(Company company)
    {
        return (company.Buyers ?? []).Where(buyer => buyer is not null);
    }
}

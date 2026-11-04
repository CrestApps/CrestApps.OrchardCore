using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Accounts.B2B.Reports.DataSources;

/// <summary>
/// One row per account that buys from the store: its buyers and locations, its payment terms, its price list, and its
/// credit limit and monthly budget. The credit limit and the budget are in the store's currency, as the account's editor
/// shows them, which the <c>Currency</c> field names. The punchout key is never exposed. Accounts are read from their
/// published version.
/// </summary>
public sealed class B2BAccountsReportDataSet : IndexedReportDataSet<ContentItem, B2BAccountIndex, B2BAccount>, IB2BAccountReportDataSet
{
    private readonly ISiteService _siteService;
    private string _storeCurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="B2BAccountsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service the store's currency is read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public B2BAccountsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IStringLocalizer<B2BAccountsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.B2BAccountSource.B2BAccounts,
                stringLocalizer["B2B accounts"],
                stringLocalizer["One row per account that buys from the store: its buyers, locations, payment terms, price list, and its credit limit and monthly budget in the store's currency."]),
            session,
            authorizationService,
            collection: null,
            B2BAccountsPermissions.ManageB2BAccounts)
    {
        _siteService = siteService;

        var S = stringLocalizer;
        var account = S["Business account"].Value;
        var terms = S["Payment terms"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inStoreCurrency = S["In the store's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Account ID"], DataFieldType.Text, record => record.AccountId, account).Identifier();
        AddField(nameof(B2BAccount.Name), S["Name"], DataFieldType.Text, record => record.Name, account);
        AddField(nameof(B2BAccount.TaxId), S["Tax or VAT number"], DataFieldType.Text, record => record.TaxId, account);
        AddField(nameof(B2BAccount.BillingEmail), S["Billing email"], DataFieldType.Text, record => record.BillingEmail, account);
        AddField(nameof(B2BAccount.IsEnabled), S["On"], DataFieldType.Boolean, record => record.IsEnabled, account);
        AddField("BuyerCount", S["Buyers"], DataFieldType.Integer, record => Buyers(record).Count(), account);
        AddField("AdminCount", S["Admins"], DataFieldType.Integer, record => Buyers(record).Count(buyer => buyer.Role == AccountMemberRole.Admin), account);
        AddField("LocationCount", S["Locations"], DataFieldType.Integer, record => record.Locations?.Count(location => location is not null) ?? 0, account);
        AddField(nameof(B2BAccount.PriceListId), S["Price list ID"], DataFieldType.Text, record => record.PriceListId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.B2BAccountSource.Name, CommerceReportDataSets.B2BAccountSource.PriceLists));
        AddField("PunchoutEnabled", S["Shops by punchout"], DataFieldType.Boolean, record => record.Punchout?.Enabled == true, account);

        AddField("OffersTerms", S["Buys on terms"], DataFieldType.Boolean, record => record.Terms?.OffersTerms == true, terms);
        AddField("NetDays", S["Net days"], DataFieldType.Integer, record => record.Terms?.NetDays, terms);
        AddField("DepositPercent", S["Deposit (%)"], DataFieldType.Decimal, record => record.Terms?.DepositPercent, terms);
        AddField("RequirePurchaseOrder", S["Purchase order required"], DataFieldType.Boolean, record => record.Terms?.RequirePurchaseOrder == true, terms);

        AddField("Currency", S["Currency"], DataFieldType.Text, _ => _storeCurrency, money).Description = S["The store's currency, which the credit limit and the monthly budget are in."].Value;
        AddField("CreditLimit", S["Credit limit"], DataFieldType.Decimal, record => record.Terms?.CreditLimit, money).Description = inStoreCurrency;
        AddField(nameof(B2BAccount.MonthlyBudget), S["Monthly budget"], DataFieldType.Decimal, record => record.MonthlyBudget, money).Description = inStoreCurrency;

        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, record => record.ContentItem.CreatedUtc, dates);
        AddField("ModifiedUtc", S["Last changed"], DataFieldType.DateTime, record => record.ContentItem.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<B2BAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<B2BAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ContentItemId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<B2BAccount> GetRows(ContentItem document)
    {
        yield return new B2BAccount(document);
    }

    /// <inheritdoc/>
    protected override Task<Func<IQueryIndex<B2BAccountIndex>, IQueryIndex<B2BAccountIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        // An account has a row per version; only the published one is read.
        return Task.FromResult<Func<IQueryIndex<B2BAccountIndex>, IQueryIndex<B2BAccountIndex>>>(rows => rows.Where(index => index.Published));
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<B2BAccount> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>())?.Currency;

        await foreach (var account in base.LoadAsync(query, cancellationToken))
        {
            yield return account;
        }
    }

    private static IEnumerable<AccountBuyer> Buyers(B2BAccount account)
    {
        return (account.Buyers ?? []).Where(buyer => buyer is not null);
    }
}

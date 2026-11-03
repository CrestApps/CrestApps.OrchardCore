using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
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
/// One row per person who buys for an account: their user, their role, and the most an order they place may come to
/// before an account admin approves it, in the store's currency, which the <c>Currency</c> field names.
/// </summary>
/// <remarks>
/// The buyers are read with their account, through the accounts' index, one account at a time and newest first: the
/// index of the buyers holds a row per buyer, so reading through it would read an account once for each of its buyers.
/// A join on the buyer's user is therefore filtered after the read, not in the database.
/// </remarks>
public sealed class B2BBuyersReportDataSet : IndexedReportDataSet<ContentItem, B2BAccountIndex, ReportChildRow<B2BAccount, AccountBuyer>>, IB2BAccountReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the account a buyer buys for.
    /// </summary>
    public const string AccountIdField = "AccountId";

    private readonly ISiteService _siteService;
    private string _storeCurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="B2BBuyersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service the store's currency is read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public B2BBuyersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IStringLocalizer<B2BBuyersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.B2BAccountSource.B2BBuyers,
                stringLocalizer["B2B buyers"],
                stringLocalizer["One row per person who buys for an account: the account, the user, their role, and their order limit in the store's currency."]),
            session,
            authorizationService,
            collection: null,
            B2BAccountsPermissions.ManageB2BAccounts)
    {
        _siteService = siteService;

        var S = stringLocalizer;
        var account = S["Business account"].Value;
        var buyer = S["Buyer"].Value;

        AddField(AccountIdField, S["Account ID"], DataFieldType.Text, row => row.Parent.AccountId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.B2BAccountSource.Name, CommerceReportDataSets.B2BAccountSource.B2BAccounts));
        AddField("AccountName", S["Business account"], DataFieldType.Text, row => row.Parent.Name, account);
        AddField("B2BAccountEnabled", S["Orders on account"], DataFieldType.Boolean, row => row.Parent.IsEnabled, account);

        AddField("Position", S["Buyer number"], DataFieldType.Integer, row => row.Position, buyer);
        AddField(nameof(AccountBuyer.UserId), S["User ID"], DataFieldType.Text, row => row.Child.UserId, buyer).Identifier(ReportDataReferences.User());
        AddField(nameof(AccountBuyer.UserName), S["User name"], DataFieldType.Text, row => row.Child.UserName, buyer);
        AddField(nameof(AccountBuyer.Email), S["Email"], DataFieldType.Text, row => row.Child.Email, buyer);
        AddField(nameof(AccountBuyer.Role), S["Role"], DataFieldType.Text, row => row.Child.Role, buyer);
        AddField("Currency", S["Currency"], DataFieldType.Text, _ => _storeCurrency, buyer).Description = S["The store's currency, which the order limit is in."].Value;
        AddField(nameof(AccountBuyer.OrderLimit), S["Order limit"], DataFieldType.Decimal, row => row.Child.OrderLimit, buyer).Description = S["The most an order the buyer places may come to before an account admin approves it, in the store's currency. Empty when there is no limit."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<B2BAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<B2BAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [AccountIdField] = index => index.ContentItemId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<B2BAccount, AccountBuyer>> GetRows(ContentItem document)
    {
        var account = new B2BAccount(document);

        return ReportChildRow<B2BAccount, AccountBuyer>.For(account, account.Buyers);
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
    protected override async IAsyncEnumerable<ReportChildRow<B2BAccount, AccountBuyer>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>())?.Currency;

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            yield return row;
        }
    }
}

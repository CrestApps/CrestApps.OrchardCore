using System.Linq.Expressions;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Customers.Reports.DataSources;

/// <summary>
/// One row per change to a customer's store credit: credit given by staff, as a refund or as a reward, or spent at a
/// checkout, with why, as the customer's admin page lists them. Money is in the account's own currency, which the
/// <c>Currency</c> field names. Accounts are read newest first, and each account gives its changes oldest first.
/// </summary>
public sealed class StoreCreditActivityReportDataSet : IndexedReportDataSet<StoreCreditAccount, StoreCreditAccountIndex, ReportChildRow<StoreCreditAccount, StoreCreditActivity>>, ICustomerReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the account a change belongs to.
    /// </summary>
    public const string StoreCreditAccountIdField = "StoreCreditAccountId";

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditActivityReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StoreCreditActivityReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StoreCreditActivityReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.StoreCreditActivity,
                stringLocalizer["Store credit activity"],
                stringLocalizer["One row per change to a customer's store credit: when, how much in the account's currency, the balance after it, and the checkout it came from."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ViewCustomers)
    {
        var S = stringLocalizer;
        var account = S["Store credit account"].Value;
        var change = S["Change"].Value;
        var inCurrency = S["In the account's currency."].Value;

        AddField(StoreCreditAccountIdField, S["Store credit account ID"], DataFieldType.Text, row => row.Parent.ItemId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.StoreCreditAccounts));
        AddField(nameof(StoreCreditAccount.CustomerId), S["Customer ID"], DataFieldType.Text, row => row.Parent.CustomerId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
        AddField(nameof(StoreCreditAccount.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, account);

        AddField("Position", S["Change number"], DataFieldType.Integer, row => row.Position, change);
        AddField(nameof(StoreCreditActivity.CreatedUtc), S["When"], DataFieldType.DateTime, row => row.Child.CreatedUtc, change);
        AddField(nameof(StoreCreditActivity.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, change).Description = S["Positive when credit was given, negative when it was spent or taken away, in the account's currency."].Value;
        AddField(nameof(StoreCreditActivity.Balance), S["Balance after"], DataFieldType.Decimal, row => row.Child.Balance, change).Description = inCurrency;
        AddField(nameof(StoreCreditActivity.Reason), S["What"], DataFieldType.Text, row => row.Child.Reason, change);
        AddField(nameof(StoreCreditActivity.Actor), S["Changed by"], DataFieldType.Text, row => row.Child.Actor, change);
        AddField("CheckoutSessionId", S["Checkout session ID"], DataFieldType.Text, row => row.Child.SessionId, change)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StoreCreditAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StoreCreditAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [StoreCreditAccountIdField] = index => index.ItemId,
            [nameof(StoreCreditAccount.CustomerId)] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<StoreCreditAccountIndex> columns)
    {
        columns.Text(nameof(StoreCreditAccount.Currency), nameof(StoreCreditAccountIndex.Currency));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<StoreCreditAccount, StoreCreditActivity>> GetRows(StoreCreditAccount document)
    {
        return ReportChildRow<StoreCreditAccount, StoreCreditActivity>.For(document, document.Activity);
    }
}

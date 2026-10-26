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
/// One row per customer's store credit in one currency: what the store owes them, and what was given and spent. Money
/// is in the account's own currency, which the <c>Currency</c> field names. Accounts are read newest first.
/// </summary>
public sealed class StoreCreditAccountsReportDataSet : IndexedReportDataSet<StoreCreditAccount, StoreCreditAccountIndex>, ICustomerReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditAccountsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StoreCreditAccountsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StoreCreditAccountsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.StoreCreditAccounts,
                stringLocalizer["Store credit accounts"],
                stringLocalizer["One row per customer's store credit in one currency: the balance, what was given and spent in that currency, and when it changed."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ViewCustomers)
    {
        var S = stringLocalizer;
        var account = S["Store credit account"].Value;
        var inCurrency = S["In the account's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Store credit account ID"], DataFieldType.Text, record => record.ItemId, account).Identifier();
        AddField(nameof(StoreCreditAccount.CustomerId), S["Customer ID"], DataFieldType.Text, record => record.CustomerId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
        AddField(nameof(StoreCreditAccount.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, account);
        AddField(nameof(StoreCreditAccount.Balance), S["Balance"], DataFieldType.Decimal, record => record.Balance, account).Description = inCurrency;
        AddField("GivenAmount", S["Given"], DataFieldType.Decimal, record => Entries(record).Where(activity => activity.Amount > 0m).Sum(activity => activity.Amount), account).Description = S["All the credit given, by staff, as refunds or as rewards, in the account's currency."].Value;
        AddField("SpentAmount", S["Spent"], DataFieldType.Decimal, record => -Entries(record).Where(activity => activity.Amount < 0m).Sum(activity => activity.Amount), account).Description = S["All the credit spent or taken away, in the account's currency."].Value;
        AddField("ActivityCount", S["Changes"], DataFieldType.Integer, record => Entries(record).Count(), account);
        AddField(nameof(StoreCreditAccount.CreatedUtc), S["Opened"], DataFieldType.DateTime, record => record.CreatedUtc, account);
        AddField(nameof(StoreCreditAccount.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, account);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StoreCreditAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StoreCreditAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(StoreCreditAccount.CustomerId)] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<StoreCreditAccountIndex> columns)
    {
        columns.Text(nameof(StoreCreditAccount.Currency), nameof(StoreCreditAccountIndex.Currency));
    }

    private static IEnumerable<StoreCreditActivity> Entries(StoreCreditAccount account)
    {
        return (account.Activity ?? []).Where(activity => activity is not null);
    }
}

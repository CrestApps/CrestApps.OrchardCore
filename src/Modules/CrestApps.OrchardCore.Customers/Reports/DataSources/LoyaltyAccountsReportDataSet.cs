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
/// One row per customer's loyalty points account: the points they hold, and what they earned and spent. Accounts are
/// read newest first.
/// </summary>
public sealed class LoyaltyAccountsReportDataSet : IndexedReportDataSet<LoyaltyAccount, LoyaltyAccountIndex>, ICustomerReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoyaltyAccountsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LoyaltyAccountsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<LoyaltyAccountsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.LoyaltyAccounts,
                stringLocalizer["Loyalty accounts"],
                stringLocalizer["One row per customer's loyalty points: the points they hold, earned and spent, and when they changed."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ViewCustomers)
    {
        var S = stringLocalizer;
        var account = S["Loyalty account"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Loyalty account ID"], DataFieldType.Text, record => record.ItemId, account).Identifier();
        AddField(nameof(LoyaltyAccount.CustomerId), S["Customer ID"], DataFieldType.Text, record => record.CustomerId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
        AddField(nameof(LoyaltyAccount.Points), S["Points"], DataFieldType.Integer, record => record.Points, account).Description = S["The points the customer holds now."].Value;
        AddField("EarnedPoints", S["Points earned"], DataFieldType.Integer, record => Sum(record, LoyaltyActivityKind.Earned), account).Description = S["The points the customer's orders earned, before any were taken back."].Value;
        AddField("SpentPoints", S["Points spent"], DataFieldType.Integer, record => -Sum(record, LoyaltyActivityKind.Spent), account).Description = S["The points the customer spent at checkouts, before any were given back."].Value;
        AddField("ActivityCount", S["Changes"], DataFieldType.Integer, record => record.Activity?.Count(activity => activity is not null) ?? 0, account);
        AddField(nameof(LoyaltyAccount.CreatedUtc), S["Opened"], DataFieldType.DateTime, record => record.CreatedUtc, account);
        AddField(nameof(LoyaltyAccount.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, account);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<LoyaltyAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LoyaltyAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(LoyaltyAccount.CustomerId)] = index => index.CustomerId,
        };

    private static long Sum(LoyaltyAccount account, LoyaltyActivityKind kind)
    {
        return account.Activity?.Where(activity => activity?.Kind == kind).Sum(activity => (long)activity.Points) ?? 0L;
    }
}

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
/// One row per change to a customer's loyalty points: points earned on an order, taken back, spent at a checkout, given
/// back, or changed by staff, with why, as the customer's admin page lists them. Accounts are read newest first, and
/// each account gives its changes oldest first.
/// </summary>
public sealed class LoyaltyActivityReportDataSet : IndexedReportDataSet<LoyaltyAccount, LoyaltyAccountIndex, ReportChildRow<LoyaltyAccount, LoyaltyActivity>>, ICustomerReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the account a change belongs to.
    /// </summary>
    public const string LoyaltyAccountIdField = "LoyaltyAccountId";

    /// <summary>
    /// Initializes a new instance of the <see cref="LoyaltyActivityReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the accounts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LoyaltyActivityReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<LoyaltyActivityReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.LoyaltyActivity,
                stringLocalizer["Loyalty activity"],
                stringLocalizer["One row per change to a customer's loyalty points: when, what kind, how many points, the balance after it, and the order or checkout it came from."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ViewCustomers)
    {
        var S = stringLocalizer;
        var account = S["Loyalty account"].Value;
        var change = S["Change"].Value;

        AddField(LoyaltyAccountIdField, S["Loyalty account ID"], DataFieldType.Text, row => row.Parent.ItemId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.LoyaltyAccounts));
        AddField(nameof(LoyaltyAccount.CustomerId), S["Customer ID"], DataFieldType.Text, row => row.Parent.CustomerId, account)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));

        AddField("Position", S["Change number"], DataFieldType.Integer, row => row.Position, change);
        AddField(nameof(LoyaltyActivity.CreatedUtc), S["When"], DataFieldType.DateTime, row => row.Child.CreatedUtc, change);
        AddField(nameof(LoyaltyActivity.Kind), S["Kind"], DataFieldType.Text, row => row.Child.Kind, change);
        AddField(nameof(LoyaltyActivity.Points), S["Points"], DataFieldType.Integer, row => row.Child.Points, change).Description = S["Positive when points were earned or given, negative when they were spent or taken back."].Value;
        AddField(nameof(LoyaltyActivity.Balance), S["Balance after"], DataFieldType.Integer, row => row.Child.Balance, change);
        AddField(nameof(LoyaltyActivity.Reason), S["What"], DataFieldType.Text, row => row.Child.Reason, change);
        AddField(nameof(LoyaltyActivity.Actor), S["Changed by"], DataFieldType.Text, row => row.Child.Actor, change);
        AddField(nameof(LoyaltyActivity.OrderId), S["Order ID"], DataFieldType.Text, row => row.Child.OrderId, change)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField("CheckoutSessionId", S["Checkout session ID"], DataFieldType.Text, row => row.Child.SessionId, change)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<LoyaltyAccountIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LoyaltyAccountIndex, string>>>(StringComparer.Ordinal)
        {
            [LoyaltyAccountIdField] = index => index.ItemId,
            [nameof(LoyaltyAccount.CustomerId)] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<LoyaltyAccount, LoyaltyActivity>> GetRows(LoyaltyAccount document)
    {
        return ReportChildRow<LoyaltyAccount, LoyaltyActivity>.For(document, document.Activity);
    }
}

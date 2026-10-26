using System.Linq.Expressions;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Reports.DataSources;

/// <summary>
/// One row per subscription agreement: where it stands, what it bills each cycle and how often, the plan it was bought
/// from, who owns it, and the dates of its life. Money is in the subscription's own currency, which the
/// <c>Currency</c> field names. The gateway's customer identifier, the history messages and what the entitlements
/// carry are never exposed.
/// </summary>
public sealed class SubscriptionsReportDataSet : IndexedReportDataSet<Subscription, SubscriptionRecordIndex>, ISubscriptionReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the subscription was created, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the owner of the subscription.
    /// </summary>
    public const string OwnerIdField = "OwnerId";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session subscriptions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<SubscriptionsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.SubscriptionSource.Subscriptions,
                stringLocalizer["Subscriptions"],
                stringLocalizer["One row per subscription: its status, what it bills each cycle in its own currency and how often, its plan, its owner and its dates."]),
            session,
            authorizationService,
            SubscriptionConstants.SubscriptionCollectionName,
            SubscriptionPermissions.ManageSubscriptions)
    {
        var S = stringLocalizer;
        var subscription = S["Subscription"].Value;
        var subscriber = S["Subscriber"].Value;
        var billing = S["Billing"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the subscription's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Subscription ID"], DataFieldType.Text, record => record.ItemId, subscription).Identifier();
        AddField(nameof(Subscription.Title), S["Title"], DataFieldType.Text, record => record.Title, subscription);
        AddField(nameof(Subscription.Status), S["Status"], DataFieldType.Text, record => record.Status, subscription);
        AddField(nameof(Subscription.ReferenceType), S["Bought as"], DataFieldType.Text, record => record.ReferenceType, subscription).Description = S["What the checkout bought. It is Subscription for a subscription plan."].Value;
        AddField(nameof(Subscription.ReferenceId), S["Plan ID"], DataFieldType.Text, record => record.ReferenceId, subscription)
            .Identifier(CommerceReportDataSets.ProductReference())
            .Description = S["The subscription plan, a product, the subscription was bought from, when it was bought as a subscription plan."].Value;
        AddField(nameof(Subscription.ReferenceVersionId), S["Plan version ID"], DataFieldType.Text, record => record.ReferenceVersionId, subscription);
        AddField(nameof(Subscription.CheckoutSessionId), S["Checkout session ID"], DataFieldType.Text, record => record.CheckoutSessionId, subscription)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));
        AddField(nameof(Subscription.ProviderKey), S["Payment provider"], DataFieldType.Text, record => record.ProviderKey, subscription);
        AddField(nameof(Subscription.ProviderSubscriptionId), S["Gateway reference"], DataFieldType.Text, record => record.ProviderSubscriptionId, subscription);
        AddField(nameof(Subscription.GatewayMode), S["Gateway mode"], DataFieldType.Text, record => record.GatewayMode, subscription);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, subscription);

        AddField(OwnerIdField, S["Owner ID"], DataFieldType.Text, record => record.OwnerId, subscriber)
            .Identifier(
                ReportDataReferences.User(),
                new DataFieldReference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers, ReportsConstants.UserIdField))
            .Description = S["The user who owns the subscription. For a guest, it is the guest's own identifier, which matches no user."].Value;
        AddField("IsGuest", S["Guest"], DataFieldType.Boolean, record => record.OwnerKind == CustomerOwnerKind.Guest, subscriber);
        AddField(nameof(Subscription.GuestContactEmail), S["Guest email"], DataFieldType.Text, record => record.GuestContactEmail, subscriber);

        AddField(nameof(Subscription.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, billing);
        AddField(nameof(Subscription.Amount), S["Amount per cycle"], DataFieldType.Decimal, record => record.Amount, billing).Description = S["What each cycle bills before tax, in the subscription's currency."].Value;
        AddField(nameof(Subscription.TaxAmount), S["Tax per cycle"], DataFieldType.Decimal, record => record.TaxAmount, billing).Description = inCurrency;
        AddField(nameof(Subscription.TotalAmount), S["Total per cycle"], DataFieldType.Decimal, record => record.TotalAmount, billing).Description = S["What each cycle bills with tax, in the subscription's currency."].Value;
        AddField(nameof(Subscription.BillingDuration), S["Billing every"], DataFieldType.Integer, record => record.BillingDuration, billing).Description = S["The number of billing units in one cycle."].Value;
        AddField(nameof(Subscription.DurationType), S["Billing unit"], DataFieldType.Text, record => record.DurationType, billing);
        AddField(nameof(Subscription.BillingCycleLimit), S["Cycles agreed"], DataFieldType.Integer, record => record.BillingCycleLimit, billing).Description = S["Empty when the subscription bills until it is canceled."].Value;
        AddField(nameof(Subscription.CyclesBilled), S["Cycles billed"], DataFieldType.Integer, record => record.CyclesBilled, billing);

        AddField(CreatedUtcField, S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Subscription.CurrentPeriodStartUtc), S["Period start"], DataFieldType.DateTime, record => record.CurrentPeriodStartUtc, dates);
        AddField(nameof(Subscription.CurrentPeriodEndUtc), S["Paid through"], DataFieldType.DateTime, record => record.CurrentPeriodEndUtc, dates);
        AddField(nameof(Subscription.NextBillingUtc), S["Next billing"], DataFieldType.DateTime, record => record.NextBillingUtc, dates);
        AddField(nameof(Subscription.TrialEndsUtc), S["Trial ends"], DataFieldType.DateTime, record => record.TrialEndsUtc, dates);
        AddField(nameof(Subscription.PastDueSinceUtc), S["Past due since"], DataFieldType.DateTime, record => record.PastDueSinceUtc, dates);
        AddField(nameof(Subscription.GraceEndsUtc), S["Grace ends"], DataFieldType.DateTime, record => record.GraceEndsUtc, dates);
        AddField(nameof(Subscription.CancelAtPeriodEnd), S["Cancels at period end"], DataFieldType.Boolean, record => record.CancelAtPeriodEnd, dates);
        AddField(nameof(Subscription.CanceledUtc), S["Canceled"], DataFieldType.DateTime, record => record.CanceledUtc, dates);
        AddField(nameof(Subscription.CancellationReason), S["Cancel reason"], DataFieldType.Text, record => record.CancellationReason, dates);
        AddField(nameof(Subscription.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<SubscriptionRecordIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<SubscriptionRecordIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<SubscriptionRecordIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [OwnerIdField] = index => index.OwnerId,
            [nameof(Subscription.ReferenceId)] = index => index.ReferenceId,
            [nameof(Subscription.CheckoutSessionId)] = index => index.CheckoutSessionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<SubscriptionRecordIndex> columns)
    {
        columns
            .Enum(nameof(Subscription.Status), index => index.Status)
            .Text(nameof(Subscription.ProviderKey), nameof(SubscriptionRecordIndex.ProviderKey))
            .Text(nameof(Subscription.Currency), nameof(SubscriptionRecordIndex.Currency));
    }
}

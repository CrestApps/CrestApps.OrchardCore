using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Entities;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// One row per checkout a buyer started, abandoned checkouts included: where it stands, the step the buyer reached,
/// what it was for, what it came to, and when it was started, completed or expired. Money is in the checkout's own
/// currency, which the <c>Currency</c> field names, and is empty for a checkout whose invoice was never built. The
/// buyer's contact details, address, IP address, browser and what they typed are never exposed: only their account and
/// the country and region they are billed in, which an anonymized checkout keeps.
/// </summary>
public sealed class CheckoutSessionsReportDataSet : IndexedReportDataSet<CheckoutSession, CheckoutSessionIndex>, ICheckoutReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the checkout was started, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the account the checkout belongs to.
    /// </summary>
    public const string UserIdField = "UserId";

    private readonly ConditionalWeakTable<CheckoutSession, SessionDetails> _details = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutSessionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session checkouts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckoutSessionsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CheckoutSessionsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CheckoutSource.CheckoutSessions,
                stringLocalizer["Checkout sessions"],
                stringLocalizer["One row per checkout a buyer started, abandoned ones included: its status, the step reached, what it was for, its amounts in its own currency, and when it was started, completed or expired."]),
            session,
            authorizationService,
            null,
            CheckoutReportPermissions.ManageOrders)
    {
        var S = stringLocalizer;
        var checkout = S["Checkout"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var buyer = S["Buyer"].Value;
        var inCurrency = S["In the checkout's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Checkout session ID"], DataFieldType.Text, record => record.SessionId, checkout).Identifier();
        AddField(nameof(CheckoutSession.Status), S["Status"], DataFieldType.Text, record => record.Status, checkout);
        AddField(nameof(CheckoutSession.CurrentStep), S["Step reached"], DataFieldType.Text, record => record.CurrentStep, checkout);
        AddField(nameof(CheckoutSession.ReferenceType), S["Bought for"], DataFieldType.Text, record => record.ReferenceType, checkout);
        AddField(nameof(CheckoutSession.ReferenceId), S["Reference ID"], DataFieldType.Text, record => record.ReferenceId, checkout).Identifier();
        AddField("OrderId", S["Order ID"], DataFieldType.Text, record => OrderId(record), checkout)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(CheckoutSession.SettlesExistingBalance), S["Pays a balance owed"], DataFieldType.Boolean, record => record.SettlesExistingBalance, checkout);
        AddField(nameof(CheckoutSession.SavePaymentMethod), S["Keeps the payment method"], DataFieldType.Boolean, record => record.SavePaymentMethod, checkout);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => Details(record).Invoice?.LineItems?.Length, checkout);
        AddField("ItemCount", S["Units"], DataFieldType.Integer, record => Details(record).Invoice?.LineItems?.Sum(line => line?.Quantity ?? 0), checkout);

        AddField(nameof(CheckoutSession.Currency), S["Currency"], DataFieldType.Text, record => record.Currency ?? Details(record).Invoice?.Currency, money);
        AddField("Total", S["Total"], DataFieldType.Decimal, record => Details(record).Invoice?.GrandTotal, money).Description = inCurrency;
        AddField("DueNow", S["Due now"], DataFieldType.Decimal, record => Details(record).Invoice?.DueNow, money).Description = S["What the buyer pays at this checkout, before any balance they use, in the checkout's currency."].Value;
        AddField("TaxAmount", S["Tax"], DataFieldType.Decimal, record => Details(record).Invoice?.TaxAmount, money).Description = inCurrency;
        AddField("DiscountTotal", S["Discounts"], DataFieldType.Decimal, record => Details(record).Invoice?.DiscountTotal, money).Description = inCurrency;
        AddField("TenderedAmount", S["Paid from balances"], DataFieldType.Decimal, record => Details(record).Invoice?.TenderTotal, money).Description = S["What gift cards and store credit paid, in the checkout's currency."].Value;
        AddField("AmountDue", S["Left to pay"], DataFieldType.Decimal, record => Details(record).Invoice?.AmountDue, money).Description = S["The total less what balances paid, in the checkout's currency."].Value;

        AddField(CreatedUtcField, S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(CheckoutSession.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(CheckoutSession.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, dates);
        AddField("ExpiredUtc", S["Expired"], DataFieldType.DateTime, record => record.Status == CheckoutSessionStatus.Expired ? (DateTime?)record.ModifiedUtc : null, dates)
            .Description = S["When a checkout nobody finished was expired."].Value;
        AddField("IsAnonymized", S["Anonymized"], DataFieldType.Boolean, record => record.AnonymizedUtc.HasValue, dates);

        AddField(UserIdField, S["User ID"], DataFieldType.Text, record => record.OwnerId, buyer).Identifier(ReportDataReferences.User());
        AddField("BillingCountry", S["Billing country"], DataFieldType.Text, record => Details(record).Contact?.BillingAddress?.Country, buyer);
        AddField("BillingRegion", S["Billing region"], DataFieldType.Text, record => Details(record).Contact?.BillingAddress?.Region, buyer);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CheckoutSessionIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CheckoutSessionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CheckoutSessionIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.SessionId,
            [UserIdField] = index => index.OwnerId,
            [nameof(CheckoutSession.ReferenceId)] = index => index.ReferenceId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CheckoutSessionIndex> columns)
    {
        columns
            .Enum(nameof(CheckoutSession.Status), index => index.Status)
            .Text(nameof(CheckoutSession.ReferenceType), nameof(CheckoutSessionIndex.ReferenceType));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The identifier and the account are kept in columns of other names.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = nameof(CheckoutSessionIndex.SessionId),
            [UserIdField] = nameof(CheckoutSessionIndex.OwnerId),
        };

        return columns;
    }

    private static string OrderId(CheckoutSession session)
    {
        return string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal)
            ? session.ReferenceId
            : null;
    }

    // The invoice and the contact are read from the session's properties once per session, not once per field.
    private SessionDetails Details(CheckoutSession session)
    {
        return _details.GetValue(session, SessionDetails.Read);
    }

    private sealed class SessionDetails
    {
        public CheckoutInvoice Invoice { get; private init; }

        public CheckoutContactInfo Contact { get; private init; }

        public static SessionDetails Read(CheckoutSession session)
        {
            return new SessionDetails
            {
                Invoice = session.TryGet<CheckoutInvoice>(out var invoice) ? invoice : null,
                Contact = session.TryGet<CheckoutContactInfo>(out var contact) ? contact : null,
            };
        }
    }
}

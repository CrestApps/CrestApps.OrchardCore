using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Transactions.Reports.DataSources;

/// <summary>
/// One row per payment the checkout tried to take, as the payments ledger lists it: the amount expected and the amount
/// the gateway confirmed, the payment method, the state, and what the payment was for. Money is in the payment's own
/// currency, which the <c>Currency</c> field names. The keys the payment is made with are never exposed.
/// </summary>
public sealed class PaymentAttemptsReportDataSet : IndexedReportDataSet<PaymentAttempt, PaymentAttemptIndex>, ITransactionReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the payment was started, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAttemptsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session payments are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentAttemptsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PaymentAttemptsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.TransactionSource.PaymentAttempts,
                stringLocalizer["Payments"],
                stringLocalizer["One row per payment the checkout tried to take: the amounts expected and confirmed in its currency, the payment method, its state and what it paid for."]),
            session,
            authorizationService,
            null,
            TransactionsPermissions.ManageRefunds)
    {
        var S = stringLocalizer;
        var payment = S["Payment"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the payment's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Payment ID"], DataFieldType.Text, record => record.ItemId, payment).Identifier();
        AddField(nameof(PaymentAttempt.State), S["State"], DataFieldType.Text, record => record.State, payment);
        AddField(nameof(PaymentAttempt.ProviderKey), S["Payment method"], DataFieldType.Text, record => record.ProviderKey, payment);
        AddField(nameof(PaymentAttempt.GatewayMode), S["Gateway mode"], DataFieldType.Text, record => record.GatewayMode, payment);
        AddField(nameof(PaymentAttempt.ProviderReference), S["Gateway reference"], DataFieldType.Text, record => record.ProviderReference, payment);
        AddField(nameof(PaymentAttempt.FailureReason), S["Why it failed"], DataFieldType.Text, record => record.FailureReason, payment);
        AddField(nameof(PaymentAttempt.IsSuperseded), S["Replaced by a later payment"], DataFieldType.Boolean, record => record.IsSuperseded, payment);
        AddField(nameof(PaymentAttempt.RiskLevel), S["Payment risk"], DataFieldType.Text, record => record.RiskLevel, payment);
        AddField(nameof(PaymentAttempt.SessionId), S["Checkout session ID"], DataFieldType.Text, record => record.SessionId, payment)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));
        AddField(nameof(PaymentAttempt.ReferenceType), S["Reference type"], DataFieldType.Text, record => record.ReferenceType, payment);
        AddField(nameof(PaymentAttempt.ReferenceId), S["Reference ID"], DataFieldType.Text, record => record.ReferenceId, payment).Identifier();
        AddField("TransactionId", S["Transaction ID"], DataFieldType.Text, record => TransactionReportReferences.TransactionId(record.ReferenceType, record.ReferenceId), payment)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions));
        AddField("OrderId", S["Order ID"], DataFieldType.Text, record => TransactionReportReferences.OrderId(record.ReferenceType, record.ReferenceId), payment)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));

        AddField(nameof(PaymentAttempt.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(PaymentAttempt.ExpectedAmount), S["Expected amount"], DataFieldType.Decimal, record => record.ExpectedAmount, money).Description = S["The amount asked for, before tax, in the payment's currency."].Value;
        AddField(nameof(PaymentAttempt.ExpectedTaxAmount), S["Expected tax"], DataFieldType.Decimal, record => record.ExpectedTaxAmount, money).Description = inCurrency;
        AddField(nameof(PaymentAttempt.ConfirmedAmount), S["Confirmed amount"], DataFieldType.Decimal, record => record.ConfirmedAmount, money).Description = S["The amount the gateway confirmed, before tax, in the payment's currency."].Value;
        AddField(nameof(PaymentAttempt.ConfirmedTaxAmount), S["Confirmed tax"], DataFieldType.Decimal, record => record.ConfirmedTaxAmount, money).Description = inCurrency;
        AddField("ConfirmedTotal", S["Confirmed total"], DataFieldType.Decimal, record => record.ConfirmedAmount + record.ConfirmedTaxAmount, money).Description = S["The amount and the tax the gateway confirmed, in the payment's currency."].Value;

        AddField(CreatedUtcField, S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(PaymentAttempt.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<PaymentAttemptIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PaymentAttemptIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PaymentAttemptIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(PaymentAttempt.SessionId)] = index => index.SessionId,
            [nameof(PaymentAttempt.ReferenceId)] = index => index.ReferenceId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PaymentAttemptIndex> columns)
    {
        columns
            .Enum(nameof(PaymentAttempt.State), index => index.State)
            .Text(nameof(PaymentAttempt.ProviderKey), nameof(PaymentAttemptIndex.ProviderKey))
            .Text(nameof(PaymentAttempt.ReferenceType), nameof(PaymentAttemptIndex.ReferenceType))
            .Text(nameof(PaymentAttempt.Currency), nameof(PaymentAttemptIndex.Currency));
    }
}

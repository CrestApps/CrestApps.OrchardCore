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
/// One row per refund of a payment, as the refunds ledger lists it: the amount and tax given back, the payment method,
/// the status, why it was given, and the payment and checkout it gave back. Money is in the refund's own currency,
/// which the <c>Currency</c> field names. The keys the refund is made with are never exposed.
/// </summary>
/// <remarks>
/// The refunds index has no creation date, so a date range on <see cref="CreatedUtcField"/> is applied to the rows read
/// rather than to the index: refunds are read newest first by their index rows.
/// </remarks>
public sealed class PaymentRefundsReportDataSet : IndexedReportDataSet<PaymentRefund, PaymentRefundIndex>, ITransactionReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the refund was asked for, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentRefundsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session refunds are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentRefundsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PaymentRefundsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.TransactionSource.PaymentRefunds,
                stringLocalizer["Refunds"],
                stringLocalizer["One row per refund of a payment: the amount and tax given back in its currency, the payment method, its status and reason, and the payment it gave back."]),
            session,
            authorizationService,
            null,
            TransactionsPermissions.ManageRefunds)
    {
        var S = stringLocalizer;
        var refund = S["Refund"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the refund's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Refund ID"], DataFieldType.Text, record => record.ItemId, refund).Identifier();
        AddField(nameof(PaymentRefund.Status), S["Status"], DataFieldType.Text, record => record.Status, refund);
        AddField(nameof(PaymentRefund.ProviderKey), S["Payment method"], DataFieldType.Text, record => record.ProviderKey, refund);
        AddField(nameof(PaymentRefund.GatewayMode), S["Gateway mode"], DataFieldType.Text, record => record.GatewayMode, refund);
        AddField(nameof(PaymentRefund.Reason), S["Reason"], DataFieldType.Text, record => record.Reason, refund);
        AddField(nameof(PaymentRefund.FailureCode), S["Failure code"], DataFieldType.Text, record => record.FailureCode, refund);
        AddField(nameof(PaymentRefund.FailureReason), S["Why it failed"], DataFieldType.Text, record => record.FailureReason, refund);
        AddField(nameof(PaymentRefund.OriginalAttemptId), S["Payment ID"], DataFieldType.Text, record => record.OriginalAttemptId, refund)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.PaymentAttempts));
        AddField(nameof(PaymentRefund.SessionId), S["Checkout session ID"], DataFieldType.Text, record => record.SessionId, refund)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));
        AddField(nameof(PaymentRefund.ReferenceType), S["Reference type"], DataFieldType.Text, record => record.ReferenceType, refund);
        AddField(nameof(PaymentRefund.ReferenceId), S["Reference ID"], DataFieldType.Text, record => record.ReferenceId, refund).Identifier();
        AddField("TransactionId", S["Transaction ID"], DataFieldType.Text, record => TransactionReportReferences.TransactionId(record.ReferenceType, record.ReferenceId), refund)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions));
        AddField("OrderId", S["Order ID"], DataFieldType.Text, record => TransactionReportReferences.OrderId(record.ReferenceType, record.ReferenceId), refund)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));

        AddField(nameof(PaymentRefund.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(PaymentRefund.RefundGrossAmount), S["Amount"], DataFieldType.Decimal, record => record.RefundGrossAmount, money).Description = S["What was given back, tax included, in the refund's currency."].Value;
        AddField(nameof(PaymentRefund.RefundTaxAmount), S["Tax refunded"], DataFieldType.Decimal, record => record.RefundTaxAmount, money).Description = inCurrency;
        AddField(nameof(PaymentRefund.RefundTaxableAmount), S["Taxable amount refunded"], DataFieldType.Decimal, record => record.RefundTaxableAmount, money).Description = inCurrency;

        AddField(CreatedUtcField, S["Asked for"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(PaymentRefund.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, dates);
        AddField(nameof(PaymentRefund.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        CompleteDescription();

        // The main date is not in the index, so it is the default date without being pushed down.
        Descriptor.DefaultDateField = CreatedUtcField;
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PaymentRefundIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PaymentRefundIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(PaymentRefund.SessionId)] = index => index.SessionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PaymentRefundIndex> columns)
    {
        columns
            .Enum(nameof(PaymentRefund.Status), index => index.Status)
            .Text(nameof(PaymentRefund.ProviderKey), nameof(PaymentRefundIndex.ProviderKey));
    }
}

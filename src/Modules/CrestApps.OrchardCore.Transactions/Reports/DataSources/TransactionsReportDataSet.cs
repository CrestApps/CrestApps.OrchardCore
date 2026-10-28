using System.Linq.Expressions;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
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
/// One row per transaction of the ledger: what is owed, paid, adjusted, refunded and still outstanding, who owes it,
/// what it is for, and where it stands. Money is in the transaction's own currency, which the <c>Currency</c> field
/// names. The outstanding transactions are those whose status is <see cref="TransactionStatus.Outstanding"/> or
/// <see cref="TransactionStatus.PartiallyPaid"/>.
/// </summary>
public sealed class TransactionsReportDataSet : IndexedReportDataSet<Transaction, TransactionIndex>, ITransactionReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the transaction was recorded, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session transactions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<TransactionsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.TransactionSource.Transactions,
                stringLocalizer["Transactions"],
                stringLocalizer["One row per transaction of the ledger: what is owed, paid and still outstanding in its own currency, its status, dates, owner and what it is for."]),
            session,
            authorizationService,
            TransactionsConstants.CollectionName,
            TransactionsPermissions.ManageTransactions)
    {
        var S = stringLocalizer;
        var transaction = S["Transaction"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var owner = S["Owner"].Value;
        var inCurrency = S["In the transaction's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Transaction ID"], DataFieldType.Text, record => record.ItemId, transaction).Identifier();
        AddField(nameof(Transaction.Title), S["Title"], DataFieldType.Text, record => record.Title, transaction);
        AddField(nameof(Transaction.Status), S["Status"], DataFieldType.Text, record => record.Status, transaction);
        AddField(nameof(Transaction.Source), S["Source"], DataFieldType.Text, record => record.Source, transaction);
        AddField(nameof(Transaction.InvoiceNumber), S["Invoice number"], DataFieldType.Text, record => record.InvoiceNumber, transaction);
        AddField(nameof(Transaction.ReferenceType), S["Reference type"], DataFieldType.Text, record => record.ReferenceType, transaction);
        AddField(nameof(Transaction.ReferenceId), S["Reference ID"], DataFieldType.Text, record => record.ReferenceId, transaction).Identifier();
        AddField("OrderId", S["Order ID"], DataFieldType.Text, record => TransactionReportReferences.OrderId(record.ReferenceType, record.ReferenceId), transaction)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(Transaction.ParentReferenceType), S["Parent reference type"], DataFieldType.Text, record => record.ParentReferenceType, transaction);
        AddField(nameof(Transaction.ParentReferenceId), S["Parent reference ID"], DataFieldType.Text, record => record.ParentReferenceId, transaction).Identifier();
        AddField(nameof(Transaction.CheckoutSessionId), S["Checkout session ID"], DataFieldType.Text, record => record.CheckoutSessionId, transaction)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CheckoutSource.Name, CommerceReportDataSets.CheckoutSource.CheckoutSessions));
        AddField("IsRecurring", S["Recurring"], DataFieldType.Boolean, record => record.Recurrence is not null, transaction);
        AddField("IsAutoCollected", S["Collected automatically"], DataFieldType.Boolean, record => record.AutoCollection is not null, transaction);
        AddField(nameof(Transaction.ReminderCount), S["Reminders sent"], DataFieldType.Integer, record => record.ReminderCount, transaction);

        AddField(nameof(Transaction.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(Transaction.Amount), S["Amount before tax"], DataFieldType.Decimal, record => record.Amount, money).Description = inCurrency;
        AddField(nameof(Transaction.TaxAmount), S["Tax"], DataFieldType.Decimal, record => record.TaxAmount, money).Description = inCurrency;
        AddField(nameof(Transaction.TotalAmount), S["Total"], DataFieldType.Decimal, record => record.TotalAmount, money).Description = inCurrency;
        AddField(nameof(Transaction.AmountPaid), S["Paid"], DataFieldType.Decimal, record => record.AmountPaid, money).Description = inCurrency;
        AddField(nameof(Transaction.AmountAdjusted), S["Adjusted or waived"], DataFieldType.Decimal, record => record.AmountAdjusted, money).Description = inCurrency;
        AddField(nameof(Transaction.AmountRefunded), S["Refunded"], DataFieldType.Decimal, record => record.AmountRefunded, money).Description = inCurrency;
        AddField(nameof(Transaction.OutstandingAmount), S["Outstanding"], DataFieldType.Decimal, record => CurrencyScale.Round(record.OutstandingAmount, record.Currency), money).Description = S["What is still owed: the total less what was adjusted and paid, rounded to the currency, in the transaction's currency."].Value;
        AddField(nameof(Transaction.SettlementMethod), S["Settled by"], DataFieldType.Text, record => record.SettlementMethod, money);

        AddField(CreatedUtcField, S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Transaction.DueUtc), S["Due"], DataFieldType.DateTime, record => record.DueUtc, dates);
        AddField(nameof(Transaction.BecameOverdueUtc), S["Became overdue"], DataFieldType.DateTime, record => record.BecameOverdueUtc, dates);
        AddField(nameof(Transaction.SettledUtc), S["Settled"], DataFieldType.DateTime, record => record.SettledUtc, dates);
        AddField(nameof(Transaction.LastReminderSentUtc), S["Last reminder"], DataFieldType.DateTime, record => record.LastReminderSentUtc, dates);
        AddField(nameof(Transaction.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        AddField(nameof(Transaction.OwnerId), S["Owner ID"], DataFieldType.Text, record => record.OwnerId, owner).Identifier();
        AddField(nameof(Transaction.OwnerKind), S["Owner kind"], DataFieldType.Text, record => record.OwnerKind, owner);
        AddField("IsGuest", S["Guest"], DataFieldType.Boolean, record => record.OwnerKind == CustomerOwnerKind.Guest, owner);
        AddField("UserId", S["User ID"], DataFieldType.Text, record => record.OwnerKind == CustomerOwnerKind.Guest ? null : record.OwnerId, owner)
            .Identifier(ReportDataReferences.User());
        AddField(nameof(Transaction.GuestContactName), S["Guest name"], DataFieldType.Text, record => record.GuestContactName, owner);
        AddField(nameof(Transaction.GuestContactEmail), S["Guest email"], DataFieldType.Text, record => record.GuestContactEmail, owner);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<TransactionIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<TransactionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TransactionIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Transaction.OwnerId)] = index => index.OwnerId,
            [nameof(Transaction.ReferenceId)] = index => index.ReferenceId,
            [nameof(Transaction.CheckoutSessionId)] = index => index.CheckoutSessionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<TransactionIndex> columns)
    {
        columns
            .Enum(nameof(Transaction.Status), index => index.Status)
            .Enum(nameof(Transaction.OwnerKind), index => index.OwnerKind)
            .Text(nameof(Transaction.Source), nameof(TransactionIndex.Source))
            .Text(nameof(Transaction.ReferenceType), nameof(TransactionIndex.ReferenceType))
            .Text(nameof(Transaction.Currency), nameof(TransactionIndex.Currency));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index cuts the parent reference to its column's length, so neither column is always the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Transaction.ParentReferenceType));
        columns.Remove(nameof(Transaction.ParentReferenceId));

        return columns;
    }
}

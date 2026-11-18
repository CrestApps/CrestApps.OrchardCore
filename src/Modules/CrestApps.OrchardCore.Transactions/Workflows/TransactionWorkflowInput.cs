using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Builds the input a transaction workflow event starts with, and the output a transaction task writes, so every
/// activity reads the same names: <c>Workflow.Input.TransactionId</c>, <c>Workflow.Input.OutstandingAmount</c> and so on.
/// </summary>
internal static class TransactionWorkflowInput
{
    public const string TransactionId = "TransactionId";

    public const string Source = "Source";

    public const string ReferenceType = "ReferenceType";

    /// <summary>
    /// Returns the values of the transaction a workflow can read. Amounts are numbers in the transaction's currency and
    /// times are UTC, as in every machine format.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    public static Dictionary<string, object> ToDictionary(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var payer = transaction.GetPayer();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [TransactionId] = transaction.ItemId,
            ["Title"] = transaction.Title,
            [Source] = transaction.Source,
            ["Status"] = transaction.Status.ToString(),
            ["Currency"] = transaction.Currency,
            ["TotalAmount"] = transaction.TotalAmount,
            ["AmountPaid"] = transaction.AmountPaid,
            ["AmountAdjusted"] = transaction.AmountAdjusted,
            ["AmountRefunded"] = transaction.AmountRefunded,
            ["OutstandingAmount"] = transaction.OutstandingAmount,
            ["OwnerId"] = transaction.OwnerId,
            ["PayerKind"] = payer.IsEmpty ? null : payer.Kind,
            ["PayerId"] = payer.IsEmpty ? null : payer.Id,
            [ReferenceType] = transaction.ReferenceType,
            ["ReferenceId"] = transaction.ReferenceId,
            ["InvoiceNumber"] = transaction.InvoiceNumber,
            ["DueUtc"] = transaction.DueUtc,
        };
    }

    /// <summary>
    /// Returns the input of an event about one entry on the transaction's timeline, such as a payment or a refund.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="entry">The entry.</param>
    public static Dictionary<string, object> ToDictionary(Transaction transaction, TransactionEvent entry)
    {
        var input = ToDictionary(transaction);

        if (entry is not null)
        {
            input["EventId"] = entry.Id;
            input["Amount"] = entry.Amount;
            input["Method"] = entry.Method;
            input["Reference"] = entry.Reference;
        }

        return input;
    }
}

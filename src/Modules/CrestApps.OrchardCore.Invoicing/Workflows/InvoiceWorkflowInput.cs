using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Workflows;

/// <summary>
/// Builds the input an invoice workflow event starts with, and the output an invoice task writes, so every activity reads
/// the same names: <c>Workflow.Input.InvoiceId</c>, <c>Workflow.Input.AmountDue</c> and so on.
/// </summary>
internal static class InvoiceWorkflowInput
{
    public const string InvoiceId = "InvoiceId";

    /// <summary>
    /// Returns the values of the invoice a workflow can read. Amounts are numbers in the invoice's currency, dates are
    /// ISO dates and times are UTC, as in every machine format.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    public static Dictionary<string, object> ToDictionary(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var party = invoice.BillTo?.Party ?? default;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [InvoiceId] = invoice.ItemId,
            ["Number"] = invoice.Number,
            ["Kind"] = invoice.Kind.ToString(),
            ["Status"] = invoice.Status.ToString(),
            ["Settlement"] = invoice.Settlement.ToString(),
            ["Delivery"] = invoice.Delivery.ToString(),
            ["Currency"] = invoice.Currency,
            ["Total"] = invoice.Totals?.Total ?? 0m,
            ["TaxTotal"] = invoice.Totals?.TaxTotal ?? 0m,
            ["AmountPaid"] = invoice.AmountPaid,
            ["AmountCredited"] = invoice.AmountCredited,
            ["AmountDue"] = invoice.AmountDue,
            ["BillToKind"] = party.IsEmpty ? null : party.Kind,
            ["BillToId"] = party.IsEmpty ? null : party.Id,
            ["BillToName"] = invoice.BillTo?.Contact?.Name,
            ["BillToEmail"] = invoice.BillTo?.Contact?.Email,
            ["IssueDate"] = invoice.IssueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["DueDate"] = invoice.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["TransactionId"] = invoice.TransactionId,
            ["SourceType"] = invoice.SourceType,
            ["SourceId"] = invoice.SourceId,
            ["OwnerId"] = invoice.OwnerId,
        };
    }
}

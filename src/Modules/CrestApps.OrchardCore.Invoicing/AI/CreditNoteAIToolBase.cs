using System.Globalization;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// What <c>previewCreditNote</c> and <c>issueCreditNote</c> share: the dispositions they offer (the ones the credit note
/// editor offers), the fingerprint of a prepared draft, which the preview's token is bound to, and how a draft and its
/// effect are described. The fingerprint is read from the stored draft, so a draft someone changed on its page after the
/// preview, or a ledger whose limit for the disposition changed, no longer matches the token.
/// </summary>
internal abstract class CreditNoteAIToolBase : InvoiceMoneyAIToolBase
{
    /// <summary>
    /// The name of the preview tool, which the issue tool sends the model back to.
    /// </summary>
    protected const string PreviewToolName = "previewCreditNote";

    /// <summary>
    /// The fingerprint family of a credit note's issue.
    /// </summary>
    protected const string FingerprintFamily = "credit-note-issue";

    /// <summary>
    /// Reads a disposition as the tools name it: <c>reduceBalance</c>, <c>refund</c> or <c>customerCredit</c>, the three the
    /// credit note editor offers.
    /// </summary>
    /// <param name="value">The value given.</param>
    /// <returns>The disposition, or <see langword="null"/> when it is not one of the three.</returns>
    protected static CreditNoteDisposition? ParseDisposition(string value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "reducebalance" => CreditNoteDisposition.ReduceBalance,
            "refund" => CreditNoteDisposition.Refund,
            "customercredit" => CreditNoteDisposition.CustomerCredit,
            _ => null,
        };

    /// <summary>
    /// The fingerprint of a prepared draft: its id, invoice, total, currency, disposition and lines, and the ledger's limit
    /// for its disposition.
    /// </summary>
    /// <param name="draft">The draft credit note, as stored.</param>
    /// <param name="limit">The most its disposition can take on the ledger now, or <see langword="null"/>.</param>
    /// <returns>The fingerprint.</returns>
    protected static string CreditNoteFingerprint(CreditNote draft, decimal? limit)
        => Fingerprint(
            FingerprintFamily,
            draft.ItemId,
            draft.InvoiceId,
            draft.Totals?.Total ?? 0m,
            draft.Currency,
            draft.Disposition.ToString(),
            string.Join(';', (draft.Lines ?? []).Select(line => string.Join(
                ':',
                line.CreditedLineId,
                Number(line.Quantity),
                Number(line.UnitPrice),
                Number(line.DiscountPercent ?? 0m),
                Number(line.DiscountAmount ?? 0m),
                Number(line.Total)))),
            limit);

    /// <summary>
    /// The lines of a credit note, for an answer.
    /// </summary>
    /// <param name="creditNote">The credit note.</param>
    /// <returns>The lines.</returns>
    protected static object[] DescribeLines(CreditNote creditNote)
        => [.. (creditNote.Lines ?? []).Where(line => line.HasAmount).Select(line => (object)new
        {
            name = line.Name,
            quantity = line.Quantity,
            unitPrice = line.UnitPrice,
            tax = line.TaxAmount,
            total = line.Total,
            creditsLineId = line.CreditedLineId,
        })];

    /// <summary>
    /// What issuing a credit note would do, in a sentence.
    /// </summary>
    /// <param name="creditNote">The credit note.</param>
    /// <param name="invoice">The invoice it credits.</param>
    /// <param name="transaction">The invoice's ledger transaction, or <see langword="null"/>.</param>
    /// <returns>The sentence.</returns>
    protected static string DescribeEffect(CreditNote creditNote, Invoice invoice, Transaction transaction)
    {
        var total = creditNote.Totals?.Total ?? 0m;
        var currency = creditNote.Currency;

        return creditNote.Disposition switch
        {
            CreditNoteDisposition.ReduceBalance => $"Lowers what invoice {invoice.Number} owes from {Format(transaction?.OutstandingAmount ?? 0m, currency)} to {Format(Math.Max(0m, (transaction?.OutstandingAmount ?? 0m) - total), currency)}.",
            CreditNoteDisposition.Refund => $"Gives {Format(total, currency)} back through the payment invoice {invoice.Number} was paid with; the payment provider refunds it, which cannot be taken back.",
            CreditNoteDisposition.CustomerCredit => $"Adds {Format(total, currency)} to the customer's credit, to use on a later invoice.",
            _ => $"Records {Format(total, currency)} as already given back.",
        };
    }

    /// <summary>
    /// Why the ledger can't take a credit note's total with its disposition, in the credit note service's words, or
    /// <see langword="null"/> when it can.
    /// </summary>
    /// <param name="disposition">The disposition.</param>
    /// <param name="limit">The ledger's limit for it.</param>
    /// <param name="total">The credit note's total.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The sentence, or <see langword="null"/>.</returns>
    protected static string DescribeDispositionLimit(CreditNoteDisposition disposition, decimal? limit, decimal total, string currency)
    {
        if (limit is not { } most || most >= total)
        {
            return null;
        }

        return disposition == CreditNoteDisposition.ReduceBalance
            ? $"Only {Format(most, currency)} is still owed on this invoice, so the credit note can't lower the balance by {Format(total, currency)}. Refund it or keep it as the customer's credit instead."
            : $"Only {Format(most, currency)} was paid on this invoice, so {Format(total, currency)} can't be refunded.";
    }

    /// <summary>
    /// Why a credit note goes past what is left to credit, in the credit note service's words.
    /// </summary>
    /// <param name="error">The problem the rules found.</param>
    /// <param name="invoice">The invoice.</param>
    /// <param name="creditNote">The credit note.</param>
    /// <returns>The sentence.</returns>
    protected static string DescribeRule(CreditNoteError error, Invoice invoice, CreditNote creditNote)
    {
        var lineName = string.IsNullOrEmpty(error.LineId)
            ? null
            : invoice.Lines.FirstOrDefault(line => string.Equals(line.Id, error.LineId, StringComparison.Ordinal))?.Name ?? error.LineId;

        return error.Code switch
        {
            CreditNoteRules.NothingCredited => "The credit note credits nothing. Keep at least one line with an amount.",
            CreditNoteRules.UnknownLine => $"A line of the credit note credits a line that is not on invoice {invoice.Number}.",
            CreditNoteRules.LineExceeded => $"\"{lineName}\" can be credited by {Format(error.Allowed ?? 0m, creditNote.Currency)} at most.",
            CreditNoteRules.TotalExceeded => $"Invoice {invoice.Number} can be credited by {Format(error.Allowed ?? 0m, creditNote.Currency)} at most.",
            _ => error.Code,
        };
    }

    /// <summary>
    /// Gets the path of a credit note's admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="creditNoteId">The credit note's id.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string CreditNoteAdminPath(IServiceProvider services, string creditNoteId)
        => string.IsNullOrEmpty(creditNoteId)
            ? null
            : AIModuleTools.GetAdminPath(services, InvoicingConstants.Features.Area, "CreditNotesAdmin", "Detail", new RouteValueDictionary { ["itemId"] = creditNoteId });

    private static string Number(decimal value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);
}

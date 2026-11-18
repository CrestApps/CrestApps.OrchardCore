using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// What a document must hold to be issued, and what its lines may hold while it is a draft. The calculator clamps what it
/// is given so a preview always shows; these rules refuse the same inputs, so nothing out of range is ever issued.
/// </summary>
public static class InvoiceIssueRules
{
    /// <summary>
    /// Checks a draft's lines and charges: quantities above zero, a discount given either as a percentage from 0 to 100 or
    /// as an amount from zero to the line's amount, and charges that are not negative.
    /// </summary>
    /// <param name="document">The draft.</param>
    /// <returns>What is wrong, or nothing.</returns>
    public static IReadOnlyList<InvoiceIssueError> ValidateLines(InvoicingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<InvoiceIssueError>();

        foreach (var line in document.Lines ?? [])
        {
            if (line is null || line.Kind is DocumentLineKind.Text or DocumentLineKind.Section)
            {
                continue;
            }

            if (line.Quantity <= 0m)
            {
                errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.InvalidQuantity, line.Id));
            }

            if (line.DiscountPercent.HasValue && line.DiscountAmount.HasValue)
            {
                errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.TwoDiscounts, line.Id));
            }
            else if (line.DiscountPercent is decimal percent && (percent < 0m || percent > 100m))
            {
                errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.InvalidDiscount, line.Id));
            }
            else if (line.DiscountAmount is decimal amount && (amount < 0m || amount > Math.Abs(line.Quantity * line.UnitPrice)))
            {
                errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.InvalidDiscount, line.Id));
            }
        }

        foreach (var charge in document.Charges ?? [])
        {
            if (charge is null)
            {
                continue;
            }

            if ((charge.Percent is decimal percent && (percent < 0m || percent > 100m)) || charge.Amount is < 0m)
            {
                errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.InvalidCharge, charge.Id));
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks that a draft can be issued: someone to bill, a currency, at least one line with an amount, and valid lines.
    /// Run it before the final calculation; <see cref="ValidateTotals"/> checks the result.
    /// </summary>
    /// <param name="document">The draft.</param>
    /// <returns>What is wrong, or nothing.</returns>
    public static IReadOnlyList<InvoiceIssueError> ValidateForIssue(InvoicingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<InvoiceIssueError>();

        if ((document.BillTo?.Party.IsEmpty ?? true) &&
            string.IsNullOrWhiteSpace(document.BillTo?.Contact?.Name) &&
            string.IsNullOrWhiteSpace(document.BillTo?.Contact?.Email))
        {
            errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.NoBillTo));
        }

        if (string.IsNullOrWhiteSpace(document.Currency) || document.Currency.Trim().Length != 3)
        {
            errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.NoCurrency));
        }

        if (!(document.Lines ?? []).Any(line => line is not null && line.Kind is not DocumentLineKind.Text and not DocumentLineKind.Section))
        {
            errors.Add(new InvoiceIssueError(InvoiceIssueErrorCode.NoLines));
        }

        errors.AddRange(ValidateLines(document));

        return errors;
    }

    /// <summary>
    /// Checks the calculated totals: a document never totals below zero (a credit note corrects an invoice instead).
    /// </summary>
    /// <param name="document">The calculated document.</param>
    /// <returns>What is wrong, or nothing.</returns>
    public static IReadOnlyList<InvoiceIssueError> ValidateTotals(InvoicingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return (document.Totals?.Total ?? 0m) < 0m
            ? [new InvoiceIssueError(InvoiceIssueErrorCode.NegativeTotal)]
            : [];
    }
}

/// <summary>
/// Why a document can't be issued or saved.
/// </summary>
/// <param name="Code">What is wrong.</param>
/// <param name="ItemId">The line or charge it is about, if any.</param>
public sealed record InvoiceIssueError(InvoiceIssueErrorCode Code, string ItemId = null);

/// <summary>
/// What is wrong with a document.
/// </summary>
public enum InvoiceIssueErrorCode
{
    /// <summary>
    /// There is nobody to bill: no party, no name and no e-mail.
    /// </summary>
    NoBillTo,

    /// <summary>
    /// The currency is missing or not a three-letter code.
    /// </summary>
    NoCurrency,

    /// <summary>
    /// There is no line to charge for.
    /// </summary>
    NoLines,

    /// <summary>
    /// A line's quantity is not above zero.
    /// </summary>
    InvalidQuantity,

    /// <summary>
    /// A line has both a discount percentage and a discount amount.
    /// </summary>
    TwoDiscounts,

    /// <summary>
    /// A line's discount is out of range: a percentage outside 0 to 100, or an amount below zero or above the line.
    /// </summary>
    InvalidDiscount,

    /// <summary>
    /// A charge is negative or its percentage is outside 0 to 100.
    /// </summary>
    InvalidCharge,

    /// <summary>
    /// The total is below zero.
    /// </summary>
    NegativeTotal,
}

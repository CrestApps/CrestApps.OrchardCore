using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Says what is wrong with a document in words staff can act on.
/// </summary>
public static class InvoiceIssueMessages
{
    /// <summary>
    /// Describes an error, naming the line it is about by its position.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="document">The document.</param>
    /// <param name="S">The localizer the words come from.</param>
    public static string Describe(InvoiceIssueError error, InvoicingDocument document, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(S);

        var line = LinePosition(error.ItemId, document);

        return error.Code switch
        {
            InvoiceIssueErrorCode.NoBillTo => S["Choose who the invoice is billed to, or enter their name or e-mail."],
            InvoiceIssueErrorCode.NoCurrency => S["Choose the currency of the invoice."],
            InvoiceIssueErrorCode.NoLines => S["Add at least one line to charge for."],
            InvoiceIssueErrorCode.InvalidQuantity => S["The quantity of line {0} must be more than zero.", line],
            InvoiceIssueErrorCode.TwoDiscounts => S["Line {0} has a discount as a percentage and as an amount. Keep one of them.", line],
            InvoiceIssueErrorCode.InvalidDiscount => S["The discount of line {0} must be a percentage from 0 to 100, or an amount from zero to the line's amount.", line],
            InvoiceIssueErrorCode.InvalidCharge => S["A discount, shipping or surcharge can't be negative, and its percentage must be from 0 to 100."],
            InvoiceIssueErrorCode.NegativeTotal => S["The total can't be below zero. Correct an invoice with a credit note instead."],
            _ => S["The invoice can't be saved as it is."],
        };
    }

    private static int LinePosition(string lineId, InvoicingDocument document)
    {
        if (string.IsNullOrEmpty(lineId) || document?.Lines is null)
        {
            return 0;
        }

        var index = document.Lines.ToList().FindIndex(line => string.Equals(line?.Id, lineId, StringComparison.Ordinal));

        return index + 1;
    }
}

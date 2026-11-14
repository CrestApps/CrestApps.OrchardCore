using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Computes the fingerprint of what an issued document says, kept on it at issue, so a later change to what must never
/// change is detected.
/// </summary>
public static class InvoiceContentHash
{
    /// <summary>
    /// Returns the SHA-256 of the document's number, dates, currency, parties, lines, charges, taxes and totals, as
    /// lowercase hexadecimal.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    public static string Compute(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var content = new
        {
            invoice.Number,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Currency,
            invoice.PricesIncludeTax,
            invoice.BillTo,
            invoice.Issuer,
            invoice.Lines,
            invoice.Charges,
            invoice.Taxes,
            invoice.Totals,
        };

        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
    }
}

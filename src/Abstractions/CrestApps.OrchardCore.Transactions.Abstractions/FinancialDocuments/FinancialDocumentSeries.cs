using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Transactions.FinancialDocuments;

/// <summary>
/// The number series of the financial documents: one per <see cref="FinancialDocumentKind"/>, registered with the shared
/// number series so a site can choose how its receipts, invoices and credit notes are numbered.
/// </summary>
public static class FinancialDocumentSeries
{
    /// <summary>
    /// The first number of a financial document series, unless the site chose another one.
    /// </summary>
    public const long FirstNumber = 1001;

    /// <summary>
    /// Gets the name of the number series of a kind of document.
    /// </summary>
    /// <param name="kind">The kind of document.</param>
    /// <returns>The series name, such as <c>Financial.Invoice</c>.</returns>
    public static string GetName(FinancialDocumentKind kind)
        => "Financial." + kind.ToString();

    /// <summary>
    /// Gets how the numbers of a kind of document are written when the site has not chosen a format: a prefix that tells
    /// the kinds apart and the number, such as <c>INV-1001</c>.
    /// </summary>
    /// <param name="kind">The kind of document.</param>
    /// <returns>The format.</returns>
    public static NumberSeriesFormat GetDefaultFormat(FinancialDocumentKind kind)
        => new(GetPrefix(kind) + "{seq}", FirstNumber);

    private static string GetPrefix(FinancialDocumentKind kind)
        => kind switch
        {
            FinancialDocumentKind.Receipt => "R-",
            FinancialDocumentKind.Invoice => "INV-",
            FinancialDocumentKind.CreditNote => "CN-",
            FinancialDocumentKind.RefundDocument => "RF-",
            _ => string.Empty,
        };
}

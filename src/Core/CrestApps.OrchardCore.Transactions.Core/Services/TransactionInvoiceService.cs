using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="ITransactionInvoiceService"/>: invoice numbers come from the site's
/// <see cref="IFinancialDocumentNumberGenerator"/>, in the invoice series.
/// </summary>
public sealed class TransactionInvoiceService : ITransactionInvoiceService
{
    private readonly IFinancialDocumentNumberGenerator _numberGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceService"/> class.
    /// </summary>
    /// <param name="numberGenerator">The generator the invoice numbers come from.</param>
    public TransactionInvoiceService(IFinancialDocumentNumberGenerator numberGenerator)
    {
        _numberGenerator = numberGenerator;
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureInvoiceNumberAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!string.IsNullOrEmpty(transaction.InvoiceNumber) || transaction.AutoCollection is not null)
        {
            return false;
        }

        var number = await _numberGenerator.GenerateAsync(new FinancialDocumentNumberRequest(FinancialDocumentKind.Invoice), cancellationToken);

        transaction.InvoiceNumber = number.PublicToken;

        return true;
    }
}

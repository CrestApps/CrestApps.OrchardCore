using System.Threading;
using System.Threading.Tasks;

namespace CrestApps.OrchardCore.Transactions.FinancialDocuments;

/// <summary>
/// Generates tenant-scoped financial-document numbers. The Transactions feature ships a sequential generator
/// that keeps the last number of each series durably and issues the next one under a distributed lock, so numbers
/// are never duplicated across nodes; receipts are numbered with it. A domain with its own numbering rules, for
/// example a per-year invoice series, replaces it.
/// </summary>
public interface IFinancialDocumentNumberGenerator
{
    /// <summary>
    /// Generates the next document number for the requested kind and series.
    /// </summary>
    /// <param name="request">The request naming the document kind and optional series.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The issued number pairing a monotonic sequence with a non-sequential public token.</returns>
    Task<FinancialDocumentNumber> GenerateAsync(FinancialDocumentNumberRequest request, CancellationToken cancellationToken = default);
}

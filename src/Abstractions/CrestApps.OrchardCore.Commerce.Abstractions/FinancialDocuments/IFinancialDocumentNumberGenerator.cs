using System.Threading;
using System.Threading.Tasks;

namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// Generates tenant-scoped financial-document numbers. It is defined as an interface with no shipped default
/// on purpose: a correct implementation needs a durable, node-safe sequence that only exists once the Orders
/// domain owns persistence, and a speculative default would risk duplicate or reused numbers. Until an Orders
/// domain provides an implementation, the shipped receipts-only policy never requires a number, so no
/// consumer depends on this service at runtime.
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

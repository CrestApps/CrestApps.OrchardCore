using System.Threading;
using System.Threading.Tasks;

namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// Decides which financial documents a money event issues. This is the seam that keeps the document policy
/// out of the modules that move money (Checkout, Payments, Transactions): they raise a
/// <see cref="FinancialDocumentContext"/> and the policy decides the outcome. The shipped default issues a
/// receipt only; a future Orders domain can replace the policy to persist numbered invoices and credit notes
/// without changing any caller.
/// </summary>
public interface IFinancialDocumentPolicy
{
    /// <summary>
    /// Evaluates which financial documents to issue for the supplied money event.
    /// </summary>
    /// <param name="context">The context describing the documented record, currency, and money event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The immutable decision describing the documents to issue.</returns>
    Task<FinancialDocumentPolicyResult> EvaluateAsync(FinancialDocumentContext context, CancellationToken cancellationToken = default);
}

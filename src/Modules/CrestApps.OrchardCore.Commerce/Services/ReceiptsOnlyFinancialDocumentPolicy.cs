using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Commerce.FinancialDocuments;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// The shipped default <see cref="IFinancialDocumentPolicy"/>. It issues a receipt only, never persists an
/// immutable copy, and never requires a formal document number, so the printable path stays delegated to the
/// existing receipt service and no numbering or persistence is introduced before an Orders domain exists.
/// A future Orders domain can register a replacement policy that persists numbered invoices and credit notes
/// without changing any caller.
/// </summary>
public sealed class ReceiptsOnlyFinancialDocumentPolicy : IFinancialDocumentPolicy
{
    private static readonly FinancialDocumentPolicyResult _receiptOnly = new(
        [FinancialDocumentKind.Receipt],
        persistImmutableCopy: false,
        requiresFormalNumber: false);

    /// <inheritdoc/>
    public Task<FinancialDocumentPolicyResult> EvaluateAsync(FinancialDocumentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(_receiptOnly);
    }
}

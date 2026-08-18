using System.Collections.Generic;

namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// The immutable decision an <see cref="IFinancialDocumentPolicy"/> returns for a money event: which
/// document kinds to issue, whether an immutable copy is persisted, and whether a formal document number is
/// required. The shipped default returns a receipt-only, non-persisted, unnumbered decision; a future Orders
/// domain can return persisted, numbered invoice and credit-note decisions through the same contract.
/// </summary>
public sealed class FinancialDocumentPolicyResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialDocumentPolicyResult"/> class.
    /// </summary>
    /// <param name="documents">The document kinds to issue for the evaluated event.</param>
    /// <param name="persistImmutableCopy">Whether an immutable copy of each issued document is persisted.</param>
    /// <param name="requiresFormalNumber">Whether each issued document requires a formal document number.</param>
    public FinancialDocumentPolicyResult(
        IReadOnlyCollection<FinancialDocumentKind> documents,
        bool persistImmutableCopy,
        bool requiresFormalNumber)
    {
        Documents = documents ?? [];
        PersistImmutableCopy = persistImmutableCopy;
        RequiresFormalNumber = requiresFormalNumber;
    }

    /// <summary>
    /// Gets the document kinds to issue for the evaluated event.
    /// </summary>
    public IReadOnlyCollection<FinancialDocumentKind> Documents { get; }

    /// <summary>
    /// Gets a value indicating whether an immutable copy of each issued document is persisted. When
    /// <see langword="false"/>, documents are produced on demand (as receipts are today) and are not stored
    /// as legal records.
    /// </summary>
    public bool PersistImmutableCopy { get; }

    /// <summary>
    /// Gets a value indicating whether each issued document requires a formal document number obtained from an
    /// <see cref="IFinancialDocumentNumberGenerator"/>. Receipts do not require one.
    /// </summary>
    public bool RequiresFormalNumber { get; }
}

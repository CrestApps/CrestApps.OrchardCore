using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Stores credit notes in the <c>Invoicing</c> collection.
/// </summary>
public interface ICreditNoteStore : ICatalog<CreditNote>
{
    /// <summary>
    /// Returns the credit notes of an invoice, oldest first.
    /// </summary>
    Task<IReadOnlyList<CreditNote>> ListByInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an issued credit note credits an invoice.
    /// </summary>
    Task<bool> HasIssuedForInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the credit note that holds a number.
    /// </summary>
    /// <param name="number">The number, such as <c>CN-1001</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CreditNote> FindByNumberAsync(string number, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="ICreditNoteStore"/>, with concurrency checks.
/// </summary>
public sealed class CreditNoteStore : DocumentCatalog<CreditNote, CreditNoteIndex>, ICreditNoteStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreditNoteStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    public CreditNoteStore(ISession session, IClock clock)
        : base(session, clock)
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CreditNote>> ListByInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(invoiceId))
        {
            return [];
        }

        var notes = await Session.Query<CreditNote, CreditNoteIndex>(index => index.InvoiceId == invoiceId, collection: CollectionName)
            .OrderBy(index => index.IssuedUtc)
            .ListAsync(cancellationToken);

        return notes.ToArray();
    }

    /// <inheritdoc/>
    public async Task<bool> HasIssuedForInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(invoiceId))
        {
            return false;
        }

        var issued = (int)CreditNoteStatus.Issued;

        return await Session.QueryIndex<CreditNoteIndex>(index => index.InvoiceId == invoiceId && index.Status == issued, collection: CollectionName)
            .CountAsync(cancellationToken) > 0;
    }

    /// <inheritdoc/>
    public Task<CreditNote> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(number)
            ? Task.FromResult<CreditNote>(null)
            : Session.Query<CreditNote, CreditNoteIndex>(index => index.Number == number.Trim(), collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

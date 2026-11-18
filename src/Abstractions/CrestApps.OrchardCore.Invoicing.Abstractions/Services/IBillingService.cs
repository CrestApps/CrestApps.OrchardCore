using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Manages the charges kept to bill on later invoices.
/// </summary>
public interface IBillableItemManager : ICatalogManager<BillableItem>
{
}

/// <summary>
/// Something that has work or costs to bill customers: it offers what is unbilled, and records the invoice line that
/// bills each, or lets it go. Its lines carry the source type <c>billable:{Type}</c> and the item's id.
/// </summary>
public interface IBillableSource
{
    /// <summary>
    /// Gets the source's type, such as <c>charges</c> or <c>time</c>.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Lists what is unbilled for the query.
    /// </summary>
    Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(BillableQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the invoice line that bills an item. Returns why it can't be billed, or <see langword="null"/>.
    /// </summary>
    Task<string> MarkBilledAsync(string id, string invoiceId, string lineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets an item go, so it can be billed again.
    /// </summary>
    Task ReleaseAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Puts unbilled work and costs from the billable sources on draft invoices, and keeps each item's billed state in step
/// with the invoice: an item stays billed while its line is on an invoice that is not void, and is let go when the
/// invoice is voided or deleted, or the line is taken off or fully credited.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Lists what a source has unbilled for the query.
    /// </summary>
    Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(string source, BillableQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the chosen items of a source to a draft invoice as lines, and marks them billed by it.
    /// </summary>
    /// <param name="invoiceId">The draft invoice.</param>
    /// <param name="source">The source's type.</param>
    /// <param name="ids">The items to add.</param>
    /// <param name="query">The query they were listed with (the time source needs its rate).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> AddToInvoiceAsync(string invoiceId, string source, IEnumerable<string> ids, BillableQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the chosen candidates to an invoice being built, not saved yet, and marks them billed by it.
    /// </summary>
    Task<int> AddToDraftAsync(Invoice invoice, string source, IEnumerable<BillableCandidate> candidates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of every item an invoice billed whose line is no longer on it, or all of them when it is void or gone.
    /// </summary>
    Task ReconcileAsync(Invoice invoice, bool releaseAll = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of the items the given lines billed.
    /// </summary>
    Task ReleaseLinesAsync(IEnumerable<DocumentLine> lines, CancellationToken cancellationToken = default);
}

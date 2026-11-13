using CrestApps.Core.Services;
using CrestApps.OrchardCore.Disbursements.Models;

namespace CrestApps.OrchardCore.Disbursements.Services;

/// <summary>
/// Stores disbursements. Write through <see cref="IDisbursementService"/>, which keeps the status moves and tells the
/// observers.
/// </summary>
public interface IDisbursementManager : ICatalogManager<Disbursement>
{
    /// <summary>
    /// Finds a disbursement by its idempotency key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursement, or <see langword="null"/>.</returns>
    Task<Disbursement> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a disbursement by its trace number.
    /// </summary>
    /// <param name="traceNumber">The trace number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursement, or <see langword="null"/>.</returns>
    Task<Disbursement> FindByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the disbursement a check pays.
    /// </summary>
    /// <param name="checkId">The check's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursement, or <see langword="null"/>.</returns>
    Task<Disbursement> FindByCheckIdAsync(string checkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the disbursements that pay a record, oldest first.
    /// </summary>
    /// <param name="sourceKind">The record's kind.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursements.</returns>
    Task<IReadOnlyList<Disbursement>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets disbursements by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursements found.</returns>
    Task<IReadOnlyList<Disbursement>> ListAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}

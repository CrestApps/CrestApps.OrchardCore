using CrestApps.Core.Services;
using CrestApps.OrchardCore.Disbursements.Models;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Stores disbursements.
/// </summary>
public interface IDisbursementStore : ICatalog<Disbursement>
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
    /// <param name="checkId">The check.</param>
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
    /// Returns whether a disbursement leaves from or goes to a bank account.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    Task<bool> UsesBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of disbursements: only those the query's access filter allows.
    /// </summary>
    /// <param name="query">What the page shows.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of matching disbursements and the page.</returns>
    Task<(int Count, IReadOnlyList<Disbursement> Disbursements)> ListPageAsync(DisbursementQuery query, int skip, int take, CancellationToken cancellationToken = default);
}

using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Stores the check register. Write through <see cref="ICheckService"/>, which numbers checks and keeps their status
/// moves.
/// </summary>
public interface ICheckManager : ICatalogManager<CheckRecord>
{
    /// <summary>
    /// Finds a check by its bank account and number.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="number">The check number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The check, or <see langword="null"/>.</returns>
    Task<CheckRecord> FindByNumberAsync(string bankAccountId, long number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the highest check number of a bank account.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number, or <see langword="null"/> when the account has no check.</returns>
    Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the checks that pay a record, oldest first.
    /// </summary>
    /// <param name="sourceKind">The kind of the record.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The checks.</returns>
    Task<IReadOnlyList<CheckRecord>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets checks by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The checks found, in check number order.</returns>
    Task<IReadOnlyList<CheckRecord>> ListAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}

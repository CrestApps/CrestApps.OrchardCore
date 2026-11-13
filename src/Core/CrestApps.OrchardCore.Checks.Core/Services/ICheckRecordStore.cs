using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// Stores the check register.
/// </summary>
public interface ICheckRecordStore : ICatalog<CheckRecord>
{
    /// <summary>
    /// Finds a check by its bank account and number.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="number">The number.</param>
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
    /// <param name="sourceKind">The record's kind.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The checks.</returns>
    Task<IReadOnlyList<CheckRecord>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default);
}

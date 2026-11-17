using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

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

    /// <summary>
    /// Reads a page of the check register: only the checks the query's access filter allows, newest first unless the
    /// query asks for the oldest.
    /// </summary>
    /// <param name="query">What the page shows.</param>
    /// <param name="skip">How many checks to skip.</param>
    /// <param name="take">How many checks to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of matching checks and the page.</returns>
    Task<(int Count, IReadOnlyList<CheckRecord> Checks)> ListPageAsync(CheckRegisterQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists printed checks dated before a date, oldest first, such as the checks that became stale.
    /// </summary>
    /// <param name="checkDateBefore">The first check date not listed.</param>
    /// <param name="take">How many checks to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The checks.</returns>
    Task<IReadOnlyList<CheckRecord>> ListPrintedBeforeAsync(DateOnly checkDateBefore, int take, CancellationToken cancellationToken = default);
}

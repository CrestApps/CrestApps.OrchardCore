using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Services;

/// <summary>
/// Works out how agreements' hours stand.
/// </summary>
public interface IAgreementBalanceService
{
    /// <summary>
    /// Gets how an agreement's hours stand in the period that holds a day: the whole agreement for a block, the month
    /// for an allowance.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="date">A day of the period, in the agreement's time zone; <see langword="null"/> for today.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The balance, or <see langword="null"/> when the agreement covers no hours.</returns>
    Task<AgreementBalance> GetBalanceAsync(Agreement agreement, DateOnly? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the agreements of an account in force on a day.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="date">The day; <see langword="null"/> for today in each agreement's time zone.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreements, by start date.</returns>
    Task<IReadOnlyList<Agreement>> ListInForceAsync(string accountId, DateOnly? date = null, CancellationToken cancellationToken = default);
}

using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Reads and adjusts workers' time-off balances.
/// </summary>
public interface ITimeOffBalanceService
{
    /// <summary>
    /// Returns a worker's balance under every policy that applies to them on a day.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The balances.</returns>
    Task<IReadOnlyList<TimeOffBalance>> GetBalancesAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a worker's balance under a policy on a day, with what will accrue until then.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="policyId">The policy.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The balance, or <see langword="null"/> when the worker or policy doesn't exist.</returns>
    Task<TimeOffBalance> GetBalanceAsync(string workerId, string policyId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a worker's ledger under a policy, newest first.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="policyId">The policy; empty for every policy.</param>
    /// <param name="skip">How many entries to skip.</param>
    /// <param name="take">How many entries to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of entries and one page of them.</returns>
    Task<(int Count, IReadOnlyList<TimeOffLedgerEntry> Items)> PageLedgerAsync(string workerId, string policyId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a worker's balance by hand. With an idempotency key, the same change is made only once.
    /// </summary>
    /// <param name="adjustment">The change.</param>
    /// <param name="actor">Who makes it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ledger entry, or why it was refused.</returns>
    Task<PlanningResult<TimeOffLedgerEntry>> AdjustAsync(TimeOffAdjustment adjustment, string actor, CancellationToken cancellationToken = default);
}

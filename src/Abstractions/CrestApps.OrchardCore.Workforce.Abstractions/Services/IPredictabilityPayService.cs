using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Lists and decides the predictability pay workers are owed for late changes to their schedules.
/// </summary>
public interface IPredictabilityPayService
{
    /// <summary>
    /// Lists entries, newest first.
    /// </summary>
    /// <param name="query">Which entries.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of entries and one page of them.</returns>
    Task<(int Count, IReadOnlyList<PredictabilityPayEntry> Items)> PageAsync(PredictabilityPayQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an entry.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<PredictabilityPayEntry> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending entry to be paid.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="actor">Who approves it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or why it can't be approved.</returns>
    Task<PlanningResult<PredictabilityPayEntry>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waives a pending entry, such as for an emergency the law exempts.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="reason">Why it isn't owed.</param>
    /// <param name="actor">Who waives it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or why it can't be waived.</returns>
    Task<PlanningResult<PredictabilityPayEntry>> WaiveAsync(string id, string reason, string actor, CancellationToken cancellationToken = default);
}

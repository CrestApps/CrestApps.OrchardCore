using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages workers. Writes run the catalog handlers, which validate and stamp the records.
/// </summary>
public interface IWorkerManager : ICatalogManager<WorkerProfile>
{
    /// <summary>
    /// Finds the worker a user is.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The worker, or <see langword="null"/>.</returns>
    Task<WorkerProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the worker with an employee number.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The worker, or <see langword="null"/>.</returns>
    Task<WorkerProfile> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of workers, by name.
    /// </summary>
    /// <param name="query">Which workers.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<WorkerProfile> Items)> SearchAsync(WorkerQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the workers who belong to a team on a day, not counting its sub-teams.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The workers, by name.</returns>
    Task<IReadOnlyList<WorkerProfile>> ListByTeamAsync(string teamId, DateOnly date, CancellationToken cancellationToken = default);
}

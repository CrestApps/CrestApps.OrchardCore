using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Works out which teams and workers a user may see and change: every team with the Manage the whole workforce
/// permission, otherwise the teams the user manages or schedules, with their sub-teams.
/// </summary>
public interface IWorkforceScopeService
{
    /// <summary>
    /// Gets the part of the workforce a user manages.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    Task<WorkforceScope> GetManagedScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user manages a worker: the worker belongs, today, to a team in the user's scope.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    Task<bool> ManagesWorkerAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may decide on a worker's records (approve a timesheet, add or correct punches, decide time off,
    /// a shift trade or an open-shift claim): never their own, unless they hold <see cref="WorkforcePermissions.ManageAllWorkforce"/>
    /// or <see cref="WorkforcePermissions.ApproveOwnRecords"/>. It does not check the scope; check that as well.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="workerId">The worker the record belongs to.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    Task<bool> CanDecideForWorkerAsync(ClaimsPrincipal user, string workerId, CancellationToken cancellationToken = default);
}

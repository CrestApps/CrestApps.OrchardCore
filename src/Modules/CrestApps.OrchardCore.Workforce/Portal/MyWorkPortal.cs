using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// What the portal's pages share: the signed-in worker, the features that are on, and the navigation.
/// </summary>
public sealed class MyWorkPortal
{
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceInbox _inbox;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _featuresManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyWorkPortal"/> class.
    /// </summary>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="inbox">The portal inbox.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="featuresManager">The features manager.</param>
    public MyWorkPortal(IWorkerManager workerManager, IWorkforceInbox inbox, IWorkforceScopeService scopeService, IAuthorizationService authorizationService, IShellFeaturesManager featuresManager)
    {
        _workerManager = workerManager;
        _inbox = inbox;
        _scopeService = scopeService;
        _authorizationService = authorizationService;
        _featuresManager = featuresManager;
    }

    /// <summary>
    /// Returns the worker the user is, or <see langword="null"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The worker.</returns>
    public async Task<WorkerProfile> GetWorkerAsync(ClaimsPrincipal user)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? null : await _workerManager.FindByUserIdAsync(userId);
    }

    /// <summary>
    /// Builds the navigation of a page.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="worker">The worker, if the user is one.</param>
    /// <param name="active">The page.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The navigation.</returns>
    public async Task<MyWorkNavViewModel> NavAsync(ClaimsPrincipal user, WorkerProfile worker, string active, CancellationToken cancellationToken = default)
    {
        var features = (await _featuresManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var scope = await _scopeService.GetManagedScopeAsync(user, cancellationToken);

        return new MyWorkNavViewModel
        {
            Active = active,
            WorkerName = worker?.DisplayName ?? user?.Identity?.Name,
            IsWorker = worker is not null,
            Unread = worker is null ? 0 : (await _inbox.PageAsync(worker.ItemId, 0, 1, cancellationToken)).Unread,
            HasShiftPlanning = features.Contains(WorkforceConstants.Features.ShiftPlanning),
            HasTimeOff = features.Contains(WorkforceConstants.Features.TimeOff) && await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.RequestTimeOff),
            IsManager = scope.All || scope.TeamIds.Count > 0,
            HasTimeClock = worker is not null && features.Contains(WorkforceConstants.Features.TimeClock) && await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.PunchTime),
        };
    }
}

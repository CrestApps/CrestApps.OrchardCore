using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IWorkforceScopeService"/>: every team with Manage the whole workforce, otherwise the teams the
/// user manages or schedules today, with their sub-teams. The scope is worked out once per request.
/// </summary>
internal sealed class DefaultWorkforceScopeService : IWorkforceScopeService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly IClock _clock;
    private readonly Dictionary<string, WorkforceScope> _cache = new(StringComparer.Ordinal);

    public DefaultWorkforceScopeService(
        IAuthorizationService authorizationService,
        WorkerStore workerStore,
        TeamStore teamStore,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _workerStore = workerStore;
        _teamStore = teamStore;
        _clock = clock;
    }

    public async Task<WorkforceScope> GetManagedScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return WorkforceScope.None;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        WorkforceScope scope;

        if (await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.ManageAllWorkforce))
        {
            scope = WorkforceScope.Everything;
        }
        else
        {
            var worker = string.IsNullOrEmpty(userId) ? null : await _workerStore.FindByUserIdAsync(userId, cancellationToken);
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var managed = worker?.MembershipsOn(today)
                .Where(membership => membership.Role is TeamMemberRole.Manager or TeamMemberRole.Scheduler)
                .Select(membership => membership.TeamId)
                .ToArray() ?? [];

            if (managed.Length == 0)
            {
                scope = WorkforceScope.None;
            }
            else
            {
                var teams = await _teamStore.ListAsync(cancellationToken);
                var ids = new HashSet<string>(StringComparer.Ordinal);

                foreach (var teamId in managed)
                {
                    ids.UnionWith(await TeamTree.GetSubtreeIdsAsync(teams, teamId));
                }

                scope = new WorkforceScope(false, ids);
            }
        }

        _cache[userId] = scope;

        return scope;
    }

    public async Task<bool> ManagesWorkerAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var scope = await GetManagedScopeAsync(user, cancellationToken);

        if (scope.All)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);

        return worker.MembershipsOn(today).Any(membership => scope.Covers(membership.TeamId));
    }
}

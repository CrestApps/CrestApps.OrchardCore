using System.Security.Claims;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Lets those who may view work orders see their service-level timers in the ServiceLevelTimers data set, the same rule
/// as the work order pages and timeline, so work order compliance can be reported.
/// </summary>
internal sealed class WorkOrderServiceLevelTimerAccess : IServiceLevelTimerAccessProvider
{
    private readonly IAuthorizationService _authorizationService;

    public WorkOrderServiceLevelTimerAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string SubjectKind => WorkOrdersConstants.RecordKind;

    public async Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default)
        => user is not null && subjectIds is { Count: > 0 } && await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders)
            ? subjectIds.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}

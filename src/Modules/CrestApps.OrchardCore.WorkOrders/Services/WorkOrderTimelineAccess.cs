using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Says who may read and write a work order's timeline: those who may view work orders read it, and those who may
/// manage them add internal notes. Work orders have no customer-facing comments.
/// </summary>
internal sealed class WorkOrderTimelineAccess : ITimelineAccessProvider
{
    private readonly IAuthorizationService _authorizationService;

    public WorkOrderTimelineAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string SubjectKind => WorkOrdersConstants.RecordKind;

    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders);

    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => visibility == TimelineVisibility.Internal
            && string.Equals(kind, TimelineConstants.Kinds.Note, StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ManageWorkOrders);
}

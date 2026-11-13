using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Says who may read and write an order's timeline: the people who manage orders read it and add internal notes. The
/// buyer doesn't see the store's notes.
/// </summary>
internal sealed class OrderTimelineAccess : ITimelineAccessProvider
{
    private readonly IAuthorizationService _authorizationService;

    public OrderTimelineAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string SubjectKind => OrderConstants.RecordKind;

    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => _authorizationService.AuthorizeAsync(user, OrderPermissions.ManageOrders);

    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => visibility == TimelineVisibility.Internal
            && string.Equals(kind, TimelineConstants.Kinds.Note, StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, OrderPermissions.ManageOrders);
}

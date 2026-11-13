using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Says who may read and write the timeline of a content item (record kind <c>Content</c>), such as an account, a
/// contact or a lead: those who may view the item read it, and those who may edit it add notes and comments.
/// </summary>
internal sealed class ContentItemTimelineAccess : ITimelineAccessProvider
{
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;

    public ContentItemTimelineAccess(IContentManager contentManager, IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
    }

    public string SubjectKind => TimelineConstants.ContentItemKind;

    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => await _contentManager.GetAsync(subject.Id, VersionOptions.Latest) is { } contentItem
            && await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem);

    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => await _contentManager.GetAsync(subject.Id, VersionOptions.Latest) is { } contentItem
            && await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem);
}

using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Who may read and write the timeline of a content item, such as an account, a contact or a lead: internal entries
/// are for the people who may edit the item, public ones for signed-in people who may view it, and writing needs the
/// right to edit it. A visitor who isn't signed in reads nothing, even on a site where anyone may view content.
/// </summary>
internal sealed class ContentItemTimelineAccess : ITimelineAccessProvider
{
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemTimelineAccess"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public ContentItemTimelineAccess(IContentManager contentManager, IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public string SubjectKind => TimelineConstants.ContentItemKind;

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || await _contentManager.GetAsync(subject.Id, VersionOptions.Latest) is not { } contentItem)
        {
            return false;
        }

        var permission = visibility == TimelineVisibility.Internal ? CommonPermissions.EditContent : CommonPermissions.ViewContent;

        return await _authorizationService.AuthorizeAsync(user, permission, contentItem);
    }

    /// <inheritdoc/>
    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => user?.Identity?.IsAuthenticated == true
            && await _contentManager.GetAsync(subject.Id, VersionOptions.Latest) is { } contentItem
            && await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem);
}

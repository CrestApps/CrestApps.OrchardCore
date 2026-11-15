using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Who may read and write the timeline of a content item, such as an account, a contact or a lead. Staff are the people
/// who may edit every item of its type: owning the item isn't enough, so on a type its owners may edit, an owner doesn't
/// count as staff. Staff read and write internal entries. Public entries exist only on the types a module opted in with
/// <see cref="TimelineServiceCollectionExtensions.AllowPublicTimelineEntries"/>; there, signed-in people who may view the
/// item read them, and staff write them. A visitor who isn't signed in reads nothing.
/// </summary>
internal sealed class ContentItemTimelineAccess : ITimelineAccessProvider
{
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly TimelineOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemTimelineAccess"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="options">The timeline options.</param>
    public ContentItemTimelineAccess(IContentManager contentManager, IAuthorizationService authorizationService, IOptions<TimelineOptions> options)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _options = options.Value;
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

        if (visibility == TimelineVisibility.Internal)
        {
            return await IsStaffAsync(user, contentItem);
        }

        return _options.PublicContentTypes.Contains(contentItem.ContentType)
            && await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem);
    }

    /// <inheritdoc/>
    /// <remarks>The items are loaded in one query; the checks then run on each.</remarks>
    public async Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        var visible = new HashSet<string>(StringComparer.Ordinal);
        var ids = (subjectIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (user?.Identity?.IsAuthenticated != true || ids.Length == 0)
        {
            return visible;
        }

        foreach (var contentItem in await _contentManager.GetAsync(ids, VersionOptions.Latest))
        {
            var allowed = visibility == TimelineVisibility.Internal
                ? await IsStaffAsync(user, contentItem)
                : _options.PublicContentTypes.Contains(contentItem.ContentType) && await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem);

            if (allowed)
            {
                visible.Add(contentItem.ContentItemId);
            }
        }

        return visible;
    }

    /// <inheritdoc/>
    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || await _contentManager.GetAsync(subject.Id, VersionOptions.Latest) is not { } contentItem)
        {
            return false;
        }

        if (visibility == TimelineVisibility.Public && !_options.PublicContentTypes.Contains(contentItem.ContentType))
        {
            return false;
        }

        return await IsStaffAsync(user, contentItem);
    }

    // Staff may edit this item (so a handler that decides per item, such as content access control, is honored) and
    // every item of the type: asked of an item nobody owns, so the right to edit one's own items doesn't count.
    private async Task<bool> IsStaffAsync(ClaimsPrincipal user, ContentItem contentItem)
        => await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem)
        && await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, new ContentItem { ContentType = contentItem.ContentType });
}

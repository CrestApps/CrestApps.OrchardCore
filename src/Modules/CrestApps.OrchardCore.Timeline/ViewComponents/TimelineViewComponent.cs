using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Timeline.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Timeline.ViewComponents;

/// <summary>
/// Shows a record's timeline, newest first, with a composer for a note or a comment. Other modules invoke it by name
/// (<c>Timeline</c>) from their record pages. What the user may read and write comes from the record kind's
/// <see cref="ITimelineAccessProvider"/>; with no access it renders nothing.
/// </summary>
public sealed class TimelineViewComponent : ViewComponent
{
    private const int PageSize = 50;

    private readonly ITimelineService _timelineService;
    private readonly ITimelineAccessService _accessService;
    private readonly IPartyDirectory _partyDirectory;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineViewComponent"/> class.
    /// </summary>
    /// <param name="timelineService">The timeline service.</param>
    /// <param name="accessService">The timeline access service.</param>
    /// <param name="partyDirectory">The party directory, for the authors' names.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TimelineViewComponent(
        ITimelineService timelineService,
        ITimelineAccessService accessService,
        IPartyDirectory partyDirectory,
        IAuthorizationService authorizationService)
    {
        _timelineService = timelineService;
        _accessService = accessService;
        _partyDirectory = partyDirectory;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Renders the timeline.
    /// </summary>
    /// <param name="subject">The record, as <c>Kind:Id</c>.</param>
    /// <param name="title">The card's title; "Notes" when empty.</param>
    /// <param name="returnUrl">The page to come back to after a post; the current page when empty.</param>
    /// <param name="kinds">The kinds of entry to show, comma-separated; every kind when empty.</param>
    /// <returns>The view, or nothing when the user may not read the timeline.</returns>
    public async Task<IViewComponentResult> InvokeAsync(string subject, string title = null, string returnUrl = null, string kinds = null)
    {
        if (!RecordRef.TryParse(subject, out var record))
        {
            return Content(string.Empty);
        }

        var cancellationToken = HttpContext.RequestAborted;
        var user = HttpContext.User;
        var canViewPublic = await _accessService.CanViewAsync(user, record, TimelineVisibility.Public, cancellationToken);
        var canViewInternal = await _accessService.CanViewAsync(user, record, TimelineVisibility.Internal, cancellationToken);

        if (!canViewPublic && !canViewInternal)
        {
            return Content(string.Empty);
        }

        var page = await _timelineService.ListAsync(new TimelineQuery
        {
            Subject = record,
            IncludeInternal = canViewInternal,
            Kinds = string.IsNullOrWhiteSpace(kinds) ? [] : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Take = PageSize,
        }, cancellationToken);

        // A user who may only read internal entries still doesn't see public ones they may not read.
        var entries = canViewPublic
            ? page.Entries
            : [.. page.Entries.Where(entry => entry.Visibility == TimelineVisibility.Internal)];

        var names = await _partyDirectory.GetNamesAsync(entries.Where(entry => !entry.Author.IsEmpty).Select(entry => entry.Author).Distinct(), cancellationToken);

        return View(new TimelineViewModel
        {
            Subject = record.ToString(),
            Title = title,
            ReturnUrl = string.IsNullOrEmpty(returnUrl) ? HttpContext.Request.Path + HttpContext.Request.QueryString : returnUrl,
            Total = page.Total,
            CanAddNote = await _accessService.CanAddAsync(user, record, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, cancellationToken),
            CanAddComment = await _accessService.CanAddAsync(user, record, TimelineConstants.Kinds.Comment, TimelineVisibility.Public, cancellationToken),
            CanRedact = await _authorizationService.AuthorizeAsync(user, TimelinePermissions.RedactTimelineEntries),
            Entries = [.. entries.Select(entry => new TimelineEntryViewModel
            {
                Entry = entry,
                AuthorName = !entry.Author.IsEmpty && names.TryGetValue(entry.Author, out var name) && !string.IsNullOrEmpty(name)
                    ? name
                    : entry.AuthorName,
            })],
        });
    }
}

using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Timeline.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Timeline.ViewComponents;

/// <summary>
/// Shows a record's timeline, newest first and a page at a time, with a composer offering the kinds of entry the user may
/// add. Other modules invoke it by name (<c>Timeline</c>) from their record pages. What the user may read and write
/// comes from the record kind's <see cref="ITimelineAccessProvider"/>; with no access it renders nothing.
/// </summary>
public sealed class TimelineViewComponent : ViewComponent
{
    /// <summary>
    /// The number of entries on a page.
    /// </summary>
    internal const int PageSize = 50;

    /// <summary>
    /// The key, in the temporary data of the next request, of the text a refused post keeps for the composer.
    /// </summary>
    internal const string DraftKey = "CrestApps.Timeline.Draft";

    private readonly ITimelineService _timelineService;
    private readonly ITimelineAccessService _accessService;
    private readonly IPartyDirectory _partyDirectory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly TimelineOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineViewComponent"/> class.
    /// </summary>
    /// <param name="timelineService">The timeline service.</param>
    /// <param name="accessService">The timeline access service.</param>
    /// <param name="partyDirectory">The party directory, for the authors' names.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="localizerFactory">The localizer factory, for the composer's buttons.</param>
    /// <param name="options">The timeline options.</param>
    public TimelineViewComponent(
        ITimelineService timelineService,
        ITimelineAccessService accessService,
        IPartyDirectory partyDirectory,
        IAuthorizationService authorizationService,
        IStringLocalizerFactory localizerFactory,
        IOptions<TimelineOptions> options)
    {
        _timelineService = timelineService;
        _accessService = accessService;
        _partyDirectory = partyDirectory;
        _authorizationService = authorizationService;
        _localizerFactory = localizerFactory;
        _options = options.Value;
    }

    /// <summary>
    /// Renders the timeline of a record.
    /// </summary>
    /// <param name="subject">The record, as <c>Kind:Id</c>.</param>
    /// <param name="title">The card's title, or <see langword="null"/> for "Notes".</param>
    /// <param name="returnUrl">The page to come back to after a post, or <see langword="null"/> for this page.</param>
    /// <param name="kinds">The entry kinds to show, comma-separated, or <see langword="null"/> for all.</param>
    /// <returns>The card.</returns>
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

        var pageKey = PageKey(record, kinds);
        var pageNumber = int.TryParse(HttpContext.Request.Query[pageKey], out var requested) && requested > 1 ? requested : 1;

        var page = await _timelineService.ListAsync(new TimelineQuery
        {
            Subject = record,
            IncludeInternal = canViewInternal,
            Kinds = string.IsNullOrWhiteSpace(kinds) ? [] : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Skip = (pageNumber - 1) * PageSize,
            Take = PageSize,
        }, cancellationToken);

        // A user who may only read internal entries still doesn't see public ones they may not read.
        var entries = canViewPublic
            ? page.Entries
            : [.. page.Entries.Where(entry => entry.Visibility == TimelineVisibility.Internal)];

        var names = await _partyDirectory.GetNamesAsync(entries.Where(entry => !entry.Author.IsEmpty).Select(entry => entry.Author).Distinct(), cancellationToken);

        // Attachments are optional: without the feature, entries show no files and the composer takes none.
        var attachmentService = HttpContext.RequestServices.GetService<IAttachmentService>();
        var files = new Dictionary<string, Attachment>(StringComparer.Ordinal);

        if (attachmentService is not null)
        {
            var ids = entries.Where(entry => entry.RedactedUtc is null).SelectMany(entry => entry.AttachmentIds).ToList();

            foreach (var attachment in await attachmentService.GetAsync(ids, cancellationToken))
            {
                files[attachment.ItemId] = attachment;
            }
        }

        var composer = new List<TimelineComposerButton>();

        foreach (var kind in _options.ComposerKinds)
        {
            if ((kind.SubjectKinds.Count == 0 || kind.SubjectKinds.Contains(record.Kind))
                && await _accessService.CanAddAsync(user, record, kind.Kind, kind.Visibility, cancellationToken))
            {
                composer.Add(new TimelineComposerButton
                {
                    Kind = kind.Kind,
                    Visibility = kind.Visibility,
                    Text = kind.ButtonText is null ? kind.Kind : _localizerFactory.Localize(kind.ButtonText).Value,
                });
            }
        }

        var lastPage = Math.Max(1, (page.Total + PageSize - 1) / PageSize);

        return View(new TimelineViewModel
        {
            Subject = record.ToString(),
            SubjectKind = record.Kind,
            Title = title,
            ReturnUrl = string.IsNullOrEmpty(returnUrl) ? HttpContext.Request.PathBase + HttpContext.Request.Path + HttpContext.Request.QueryString : returnUrl,
            Total = page.Total,
            Composer = composer,
            Draft = TempData?.Peek(DraftKey) is string draft && draft.StartsWith(record + "\n", StringComparison.Ordinal) ? Pop(draft, record) : null,
            CanAttach = attachmentService is not null && composer.Count > 0,
            CanRedact = await _authorizationService.AuthorizeAsync(user, TimelinePermissions.RedactTimelineEntries),
            PageNumber = pageNumber,
            NewerUrl = pageNumber > 1 ? PageUrl(pageKey, pageNumber - 1) : null,
            OlderUrl = pageNumber < lastPage ? PageUrl(pageKey, pageNumber + 1) : null,
            Entries = [.. entries.Select(entry => new TimelineEntryViewModel
            {
                Entry = entry,
                AuthorName = !entry.Author.IsEmpty && names.TryGetValue(entry.Author, out var name) && !string.IsNullOrEmpty(name)
                    ? name
                    : entry.AuthorName,
                Attachments = [.. entry.AttachmentIds
                    .Where(files.ContainsKey)
                    .Select(id => files[id])
                    .Select(file => new TimelineAttachmentViewModel
                    {
                        Id = file.ItemId,
                        FileName = file.FileName,
                        Length = file.Length,
                        IsImage = file.IsImage,
                    })],
            })],
        });
    }

    /// <summary>
    /// The query key that holds the page of a record's timeline, so two timelines on one page page apart.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The key.</returns>
    /// <param name="kinds">The kinds the timeline shows, when it shows only some: a record's conversation and its
    /// history page apart.</param>
    internal static string PageKey(RecordRef record, string kinds = null)
        => "timeline-" + record.Kind + "-" + record.Id
            + (string.IsNullOrWhiteSpace(kinds) ? string.Empty : "-" + string.Join("-", kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant());

    // The text a refused post kept, read once.
    private string Pop(string draft, RecordRef record)
    {
        TempData.Remove(DraftKey);

        return draft[(record.ToString().Length + 1)..];
    }

    // This page's address with the timeline's page set, keeping the tenant's path base and the other query values.
    private string PageUrl(string pageKey, int pageNumber)
    {
        var request = HttpContext.Request;
        var query = new QueryBuilder(request.Query
            .Where(pair => !string.Equals(pair.Key, pageKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value))));

        if (pageNumber > 1)
        {
            query.Add(pageKey, pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return request.PathBase + request.Path + query.ToQueryString();
    }
}

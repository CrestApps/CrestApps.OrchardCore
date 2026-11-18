using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Shows every file of a ticket in one place: the files of its replies and notes and of the tickets merged into it,
/// newest first, each with who added it, when, and a link to the reply or note it came with. Agents see the files of
/// internal notes when they may read the notes; the requester (in the portal) sees only the files of public replies.
/// Files of redacted entries never show. Only the newest are shown until the reader asks for all.
/// </summary>
public sealed class TicketFilesViewComponent : ViewComponent
{
    /// <summary>
    /// The query string value that shows every file.
    /// </summary>
    public const string ShowAllKey = "files";

    // How many files show before "Show all", and at most with it.
    internal const int FirstFiles = 12;
    internal const int MaxFiles = 500;

    private static readonly TimeSpan _signedLinkLifetime = TimeSpan.FromHours(1);

    private readonly ISession _session;
    private readonly ITicketAccessService _access;
    private readonly IServiceProvider _services;
    private readonly AdminOptions _adminOptions;

    public TicketFilesViewComponent(ISession session, ITicketAccessService access, IServiceProvider services, IOptions<AdminOptions> adminOptions)
    {
        _session = session;
        _access = access;
        _services = services;
        _adminOptions = adminOptions.Value;
    }

    /// <summary>
    /// Shows a ticket's files.
    /// </summary>
    /// <param name="ticket">The ticket, or <see langword="null"/> to load it by <paramref name="ticketId"/>.</param>
    /// <param name="ticketId">The ticket's id, when <paramref name="ticket"/> isn't given.</param>
    /// <param name="forRequester">Whether the requester reads them, in the portal (only public files, by signed links).
    /// The portal has checked the requester may see the ticket.</param>
    /// <returns>The files, or nothing when there are none to show.</returns>
    public async Task<IViewComponentResult> InvokeAsync(ContentItem ticket = null, string ticketId = null, bool forRequester = false)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (ticket is null && !string.IsNullOrEmpty(ticketId))
        {
            ticket = await _session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == ticketId).FirstOrDefaultAsync(cancellationToken);
        }

        if (ticket is null
            || _services.GetService<ITimelineFileService>() is not { } files
            || _services.GetService<IAttachmentService>() is not { } attachments
            || (!forRequester && !await _access.CanAsync(HttpContext.User, ticket, TicketOperation.ViewAssigned, cancellationToken)))
        {
            return Content(string.Empty);
        }

        var part = ticket.GetOrCreate<TicketPart>();
        var subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId);
        var merged = await MergedTicketsAsync(ticket, part, forRequester, cancellationToken);
        RecordRef[] subjects = [subject, .. merged.Keys.Select(id => new RecordRef(TicketsConstants.RecordKind, id))];

        // Each ticket's internal files show to whoever may read that ticket's internal notes; never to the requester.
        var internalSubjects = new List<RecordRef>();

        if (!forRequester && _services.GetService<ITimelineAccessService>() is { } timelineAccess)
        {
            foreach (var record in subjects)
            {
                if (await timelineAccess.CanViewAsync(HttpContext.User, record, TimelineVisibility.Internal, cancellationToken))
                {
                    internalSubjects.Add(record);
                }
            }
        }

        var showAll = string.Equals(HttpContext.Request.Query[ShowAllKey], "all", StringComparison.OrdinalIgnoreCase);
        var page = await files.ListAsync(new TimelineFileQuery
        {
            Subjects = subjects,
            InternalSubjects = internalSubjects,
            Take = showAll ? MaxFiles : FirstFiles,
        }, cancellationToken);

        var found = (await attachments.GetAsync(page.Files.Select(file => file.AttachmentId), cancellationToken))
            .ToDictionary(attachment => attachment.ItemId, StringComparer.Ordinal);
        var model = new TicketFilesViewModel { Total = page.Total, ForRequester = forRequester };

        foreach (var file in page.Files)
        {
            if (!found.TryGetValue(file.AttachmentId, out var attachment))
            {
                continue;
            }

            var fromNumber = merged.GetValueOrDefault(file.Subject.Id);
            var item = new TicketFileViewModel
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Length = attachment.Length,
                IsImage = attachment.IsImage,
                AddedBy = file.AuthorName,
                AddedUtc = file.CreatedUtc,
                Internal = file.Visibility == TimelineVisibility.Internal,
                FromNumber = fromNumber,
            };

            if (forRequester)
            {
                var signed = attachments.CreateSignedPath(attachment.ItemId, _signedLinkLifetime);
                item.PreviewUrl = signed;
                item.DownloadUrl = signed + "&download=true";
                item.EntryUrl = fromNumber is null
                    ? "#" + Portal.TicketPortalController.EntryAnchor(file.EntryId)
                    : Url.Content("~/account/requests/" + Uri.EscapeDataString(fromNumber) + "#" + Portal.TicketPortalController.EntryAnchor(file.EntryId));
            }
            else
            {
                var path = Url.Content("~/attachments/" + Uri.EscapeDataString(attachment.ItemId));
                item.PreviewUrl = path;
                item.DownloadUrl = path + "?download=true";
                item.EntryUrl = Url.Content($"~/{_adminOptions.AdminUrlPrefix}/timeline/entry/{Uri.EscapeDataString(file.EntryId)}?kinds={Uri.EscapeDataString(TicketsConstants.EntryKinds.Reply + "," + TicketsConstants.EntryKinds.Note)}");
            }

            model.Files.Add(item);
        }

        if (!showAll && page.Total > model.Files.Count)
        {
            var query = QueryString.Create(HttpContext.Request.Query
                .Where(pair => !string.Equals(pair.Key, ShowAllKey, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value)))
                .Append(new KeyValuePair<string, string>(ShowAllKey, "all")));
            model.ShowAllUrl = HttpContext.Request.PathBase + HttpContext.Request.Path + query + "#ticket-files";
        }

        return View(model);
    }

    // The tickets merged into this one, by id, with their numbers: those the agent may see, or, for the requester, those
    // of the same requester.
    private async Task<Dictionary<string, string>> MergedTicketsAsync(ContentItem ticket, TicketPart part, bool forRequester, CancellationToken cancellationToken)
    {
        var id = ticket.ContentItemId;
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var source in await _session.Query<ContentItem, TicketIndex>(index => index.MergedIntoId == id).ListAsync(cancellationToken))
        {
            var sourcePart = source.GetOrCreate<TicketPart>();
            var allowed = forRequester
                ? !part.Requester.IsEmpty && sourcePart.Requester == part.Requester
                : await _access.CanAsync(HttpContext.User, source, TicketOperation.ViewAssigned, cancellationToken);

            if (allowed)
            {
                merged[source.ContentItemId] = sourcePart.Number;
            }
        }

        return merged;
    }
}

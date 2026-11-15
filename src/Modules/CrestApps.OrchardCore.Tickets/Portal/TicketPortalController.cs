using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// The requesters' pages: their requests in My account, a new request from the ticket types customers may submit, and a
/// request's conversation with a reply box. A guest opens one request through the signed link in its e-mails.
/// </summary>
[Feature(TicketsConstants.Features.Portal)]
public sealed class TicketPortalController : Controller
{
    /// <summary>
    /// The route of a request in My account.
    /// </summary>
    public const string DisplayRouteName = "TicketPortalDisplay";

    /// <summary>
    /// The route of a request opened from a guest's signed link.
    /// </summary>
    public const string GuestRouteName = "TicketPortalGuest";

    private const int PageSize = 20;

    private static readonly TimeSpan _fileLinkLifetime = TimeSpan.FromHours(1);

    private readonly TicketPortalAccess _access;
    private readonly ITicketService _tickets;
    private readonly ITimelineService _timeline;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly TicketUploads _uploads;
    private readonly IEnumerable<IAttachmentService> _attachments;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public TicketPortalController(
        TicketPortalAccess access,
        ITicketService tickets,
        ITimelineService timeline,
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        TicketUploads uploads,
        IEnumerable<IAttachmentService> attachments,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<TicketPortalController> htmlLocalizer)
    {
        _access = access;
        _tickets = tickets;
        _timeline = timeline;
        _desks = desks;
        _statuses = statuses;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _uploads = uploads;
        _attachments = attachments;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// The requester's requests, or their account's for an account administrator.
    /// </summary>
    /// <param name="organization">Whether to show the account's requests.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page.</returns>
    [Authorize]
    [HttpGet("account/requests", Name = "TicketPortal")]
    public async Task<IActionResult> Index(bool organization = false, int page = 1)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);

        if (viewer is null)
        {
            return Forbid();
        }

        organization &= !string.IsNullOrEmpty(viewer.AdminAccountId);
        page = Math.Max(1, page);

        var (tickets, total) = await _access.ListAsync(viewer, organization, (page - 1) * PageSize, PageSize, HttpContext.RequestAborted);
        var statuses = (await _statuses.GetAllAsync()).ToDictionary(status => status.ItemId, StringComparer.Ordinal);
        var model = new TicketPortalListViewModel
        {
            Organization = organization,
            AccountName = viewer.AdminAccountName,
            CanCreate = (await SubmittableTypesAsync()).Count > 0 && (await CustomerDesksAsync()).Count > 0,
            Page = page,
            Total = total,
            PageSize = PageSize,
        };

        foreach (var ticket in tickets)
        {
            var part = ticket.GetOrCreate<TicketPart>();

            model.Items.Add(new TicketPortalRow
            {
                Number = part.Number,
                Subject = ticket.DisplayText,
                Status = StatusLabel(statuses, part),
                IsOpen = part.StatusCategory is not (TicketStatusCategory.Solved or TicketStatusCategory.Closed),
                Created = await LocalAsync(ticket.CreatedUtc),
                RequesterName = part.RequesterContact?.Name,
            });
        }

        return View(model);
    }

    /// <summary>
    /// The form for a new request: the ticket types customers may submit, then the chosen type's form.
    /// </summary>
    /// <param name="contentType">The ticket type.</param>
    /// <returns>The page.</returns>
    [Authorize]
    [HttpGet("account/requests/new", Name = "TicketPortalCreate")]
    public async Task<IActionResult> Create(string contentType = null)
    {
        var model = new TicketPortalCreateViewModel { ContentType = contentType };

        if (!await PrepareCreateAsync(model))
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(model.ContentType))
        {
            model.Editor = await _contentItemDisplayManager.BuildEditorAsync(await _contentManager.NewAsync(model.ContentType), _updateModelAccessor.ModelUpdater, isNew: true);
        }

        return View(model);
    }

    /// <summary>
    /// Opens a request for the signed-in requester.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <param name="files">The files of the first message.</param>
    /// <returns>The new request, or the form with what's wrong.</returns>
    [Authorize]
    [HttpPost("account/requests/new")]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(TicketPortalCreateViewModel model, List<IFormFile> files)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);

        if (viewer is null || !await PrepareCreateAsync(model) || string.IsNullOrEmpty(model.ContentType))
        {
            return NotFound();
        }

        if (!model.Desks.Any(desk => desk.Value == model.DeskId))
        {
            ModelState.AddModelError(nameof(model.DeskId), H["Choose what your request is about."].Value);
        }

        if (string.IsNullOrWhiteSpace(model.Body))
        {
            ModelState.AddModelError(nameof(model.Body), H["Describe your request."].Value);
        }

        var ticket = await _contentManager.NewAsync(model.ContentType);
        model.Editor = await _contentItemDisplayManager.UpdateEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entryId = UniqueId.GenerateId();
        var (attached, errors) = await _uploads.StoreAsync(files, entryId, viewer.Party, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        var result = await _tickets.CreateAsync(new TicketCreateRequest
        {
            Ticket = ticket,
            ContentType = model.ContentType,
            Body = model.Body,
            EntryId = entryId,
            AttachmentIds = attached,
            DeskId = model.DeskId,
            Requester = viewer.Party,
            Channel = TicketsConstants.Channels.Portal,
            CreatedBy = viewer.Party,
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _uploads.DeleteAsync(attached);

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        var number = result.Ticket.GetOrCreate<TicketPart>().Number;
        await _notifier.SuccessAsync(H["We received your request {0}.", number]);

        return RedirectToRoute(DisplayRouteName, new { number });
    }

    /// <summary>
    /// A request and its conversation.
    /// </summary>
    /// <param name="number">The request's number.</param>
    /// <returns>The page.</returns>
    [Authorize]
    [HttpGet("account/requests/{number}", Name = DisplayRouteName)]
    public async Task<IActionResult> Display(string number)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);
        var ticket = await _tickets.FindByNumberAsync(number);

        if (!TicketPortalAccess.CanSee(viewer, ticket))
        {
            return NotFound();
        }

        return View(await BuildDisplayAsync(ticket, viewer.Party, guestToken: null));
    }

    /// <summary>
    /// Adds the requester's reply.
    /// </summary>
    /// <param name="number">The request's number.</param>
    /// <param name="body">The reply.</param>
    /// <param name="files">Its files.</param>
    /// <returns>The request.</returns>
    [Authorize]
    [HttpPost("account/requests/{number}/reply", Name = "TicketPortalReply")]
    public async Task<IActionResult> Reply(string number, string body, List<IFormFile> files)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);
        var ticket = await _tickets.FindByNumberAsync(number);

        if (!TicketPortalAccess.CanSee(viewer, ticket))
        {
            return NotFound();
        }

        await ReplyAsync(ticket, body, files, viewer.Party, viewer.UserName);

        return RedirectToRoute(DisplayRouteName, new { number });
    }

    /// <summary>
    /// A request opened from the signed link in a guest's e-mail.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="token">The signed token.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    [HttpGet("requests/{id}", Name = GuestRouteName)]
    public async Task<IActionResult> Guest(string id, string token)
    {
        if (!_access.IsGuestTokenFor(token, id) || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        return View(nameof(Display), await BuildDisplayAsync(ticket, default, token));
    }

    /// <summary>
    /// Adds a guest's reply from the signed link.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="token">The signed token.</param>
    /// <param name="body">The reply.</param>
    /// <param name="files">Its files.</param>
    /// <returns>The request.</returns>
    [AllowAnonymous]
    [HttpPost("requests/{id}/reply", Name = "TicketPortalGuestReply")]
    public async Task<IActionResult> GuestReply(string id, string token, string body, List<IFormFile> files)
    {
        if (!_access.IsGuestTokenFor(token, id) || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        var part = ticket.GetOrCreate<TicketPart>();
        await ReplyAsync(ticket, body, files, default, part.RequesterContact?.Name ?? part.RequesterContact?.Email);

        return RedirectToRoute(GuestRouteName, new { id, token });
    }

    private async Task ReplyAsync(ContentItem ticket, string body, List<IFormFile> files, PartyRef author, string authorName)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            await _notifier.ErrorAsync(H["Write your reply first."]);

            return;
        }

        var entryId = UniqueId.GenerateId();
        var (attached, errors) = await _uploads.StoreAsync(files, entryId, author, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return;
        }

        var result = await _tickets.ReplyAsync(ticket.ContentItemId, new TicketReply
        {
            Body = body,
            FromRequester = true,
            Author = author,
            AuthorName = authorName,
            Channel = TicketsConstants.Channels.Portal,
            EntryId = entryId,
            AttachmentIds = attached,
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _uploads.DeleteAsync(attached);

            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return;
        }

        await _notifier.SuccessAsync(H["Your reply was sent."]);
    }

    private async Task<TicketPortalDisplayViewModel> BuildDisplayAsync(ContentItem ticket, PartyRef viewer, string guestToken)
    {
        var part = ticket.GetOrCreate<TicketPart>();
        var statuses = (await _statuses.GetAllAsync()).ToDictionary(status => status.ItemId, StringComparer.Ordinal);
        var model = new TicketPortalDisplayViewModel
        {
            TicketId = ticket.ContentItemId,
            Number = part.Number,
            Subject = ticket.DisplayText,
            Status = StatusLabel(statuses, part),
            Created = await LocalAsync(ticket.CreatedUtc),

            // A merged request continues on the one it was merged into; there is nothing to answer here.
            CanReply = string.IsNullOrEmpty(part.MergedIntoId),
            CanAttach = _uploads.CanAttach,
            GuestToken = guestToken,
        };

        // Only public replies: internal notes and the record of changes are the business's own.
        var page = await _timeline.ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
            Kinds = [TicketsConstants.EntryKinds.Reply],
            IncludeInternal = false,
            Take = 200,
        }, HttpContext.RequestAborted);
        var attachments = _attachments.FirstOrDefault();

        foreach (var entry in page.Entries.Where(entry => entry.Visibility == TimelineVisibility.Public && entry.RedactedUtc is null).OrderBy(entry => entry.CreatedUtc))
        {
            var row = new TicketPortalEntry
            {
                AuthorName = entry.AuthorName,

                // The requester's own messages: theirs, or a guest's, which have no author.
                IsRequester = entry.Author.IsEmpty ? viewer.IsEmpty || part.Requester.IsEmpty : entry.Author == part.Requester,
                When = await LocalAsync(entry.CreatedUtc),
                Body = entry.Body,
            };

            if (attachments is not null && entry.AttachmentIds.Count > 0)
            {
                foreach (var file in await attachments.GetAsync(entry.AttachmentIds, HttpContext.RequestAborted))
                {
                    row.Files.Add(new TicketPortalFile { FileName = file.FileName, Url = attachments.CreateSignedPath(file.ItemId, _fileLinkLifetime) });
                }
            }

            model.Entries.Add(row);
        }

        return model;
    }

    private async Task<bool> PrepareCreateAsync(TicketPortalCreateViewModel model)
    {
        model.Types = await SubmittableTypesAsync();

        if (model.Types.Count == 0)
        {
            return false;
        }

        if (model.Types.Count == 1)
        {
            model.ContentType ??= model.Types[0].Name;
        }

        if (!string.IsNullOrEmpty(model.ContentType) && !model.Types.Any(type => type.Name == model.ContentType))
        {
            return false;
        }

        var desks = await CustomerDesksAsync();
        model.Desks = [.. desks
            .Where(desk => string.IsNullOrEmpty(model.ContentType) || desk.TicketTypes.Count == 0 || desk.TicketTypes.Contains(model.ContentType, StringComparer.OrdinalIgnoreCase))
            .OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == model.DeskId))];

        if (model.Desks.Count == 1)
        {
            model.DeskId ??= model.Desks[0].Value;
        }

        model.CanAttach = _uploads.CanAttach;

        return model.Desks.Count > 0;
    }

    // The ticket types a type's settings let customers submit.
    private async Task<List<TicketPortalType>> SubmittableTypesAsync()
        => [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Select(type => (type, part: type.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)))
            .Where(pair => pair.part?.GetSettings<TicketPartSettings>().CustomerSubmittable == true)
            .OrderBy(pair => pair.type.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(pair => new TicketPortalType
            {
                Name = pair.type.Name,
                DisplayName = pair.type.DisplayName,
                Description = pair.part.GetSettings<TicketPartSettings>().PortalDescription,
            })];

    // The desks that serve customers.
    private async Task<List<TicketDesk>> CustomerDesksAsync()
        => [.. (await _desks.GetAllAsync()).Where(desk => desk.Audience == TicketDeskAudience.Customers)];

    private static string StatusLabel(Dictionary<string, TicketStatus> statuses, TicketPart part)
        => part.StatusId is not null && statuses.TryGetValue(part.StatusId, out var status)
            ? string.IsNullOrWhiteSpace(status.CustomerLabel) ? status.Name : status.CustomerLabel
            : null;

    private async Task<string> LocalAsync(DateTime? utc)
        => utc is { } value
            ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture)
            : null;
}

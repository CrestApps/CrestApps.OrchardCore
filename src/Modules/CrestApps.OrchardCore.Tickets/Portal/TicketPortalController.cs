using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// The route of the page a requester rates a ticket on, from the signed link in the rating e-mail.
    /// </summary>
    public const string RateRouteName = "TicketPortalRate";

    private const int PageSize = 20;

    // How many replies a requester may send on one request in an hour.
    private const int MaxRepliesPerHour = 20;

    // The original request and the latest messages are shown; a long thread drops the ones between.
    private const int ThreadTake = 200;

    // Guest and rating links are traded for these cookies on first use, so the token leaves the address bar and logs.
    private const string GuestCookieName = "tickets-guest";
    private const string RatingCookieName = "tickets-rating";

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

    /// <inheritdoc/>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        // Private pages: never cached, never indexed, and no address (with a token) handed on to other sites.
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        base.OnActionExecuting(context);
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
            CanCreate = (await SubmittableTypesAsync()).Count > 0 && (await CustomerDesksAsync(viewer)).Count > 0,
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

        if (!await PrepareCreateAsync(model, await _access.GetViewerAsync(User, HttpContext.RequestAborted)))
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
    [HttpPost("account/requests/new", Name = "TicketPortalCreatePost")]
    [ActionName(nameof(Create))]
    [RequestSizeLimit(TicketUploads.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketUploads.MaxRequestBytes)]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.PortalCreate)]
    public async Task<IActionResult> CreatePost(TicketPortalCreateViewModel model, List<IFormFile> files)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);

        if (viewer is null || !await PrepareCreateAsync(model, viewer) || string.IsNullOrEmpty(model.ContentType))
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

            // Filed under the requester's account (on a desk that serves customers), so its administrators follow it.
            AccountId = (await _desks.FindByIdAsync(model.DeskId))?.Audience == TicketDeskAudience.Customers ? viewer.MemberAccountId : null,
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

        return RedirectToRoute(DisplayRouteName, new { number = result.Ticket.GetOrCreate<TicketPart>().Number, done = Done.Created });
    }

    /// <summary>
    /// A request and its conversation.
    /// </summary>
    /// <param name="number">The request's number.</param>
    /// <param name="done">What was just done, to say so.</param>
    /// <returns>The page.</returns>
    [Authorize]
    [HttpGet("account/requests/{number}", Name = DisplayRouteName)]
    public async Task<IActionResult> Display(string number, string done = null)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);
        var ticket = await _tickets.FindByNumberAsync(number);

        if (!await _access.CanSeeAsync(viewer, ticket, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var model = await BuildDisplayAsync(ticket, viewer.Party, guest: false);
        model.Message = DoneMessage(done);

        return View(model);
    }

    /// <summary>
    /// Adds the requester's reply.
    /// </summary>
    /// <param name="number">The request's number.</param>
    /// <param name="body">The reply.</param>
    /// <param name="files">Its files.</param>
    /// <returns>The request, or the page with what's wrong and the reply kept.</returns>
    [Authorize]
    [HttpPost("account/requests/{number}/reply", Name = "TicketPortalReply")]
    [RequestSizeLimit(TicketUploads.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketUploads.MaxRequestBytes)]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.PortalReply)]
    public async Task<IActionResult> Reply(string number, string body, List<IFormFile> files)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);
        var ticket = await _tickets.FindByNumberAsync(number);

        if (!await _access.CanSeeAsync(viewer, ticket, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var (sent, error) = await ReplyAsync(ticket, body, files, viewer.Party, viewer.UserName);

        if (error is not null)
        {
            var model = await BuildDisplayAsync(ticket, viewer.Party, guest: false);
            model.Error = error;
            model.Draft = Draft(body);

            return View(nameof(Display), model);
        }

        // A reply on a closed request opens a follow-up, which is where it shows.
        return RedirectToRoute(DisplayRouteName, new { number = sent.GetOrCreate<TicketPart>().Number, done = Done.Sent });
    }

    /// <summary>
    /// A request opened from the signed link in a guest's e-mail. The link's token is traded for a cookie on this page
    /// only, and the page reloads without it.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="token">The signed token, on the first visit.</param>
    /// <param name="done">What was just done, to say so.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    [HttpGet("requests/{id}", Name = GuestRouteName)]
    public async Task<IActionResult> Guest(string id, string token, string done = null)
    {
        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(token))
        {
            if (!_access.IsGuestTokenFor(token, ticket))
            {
                return NotFound();
            }

            KeepToken(GuestCookieName, id, token);

            return RedirectToRoute(GuestRouteName, new { id });
        }

        if (!_access.IsGuestTokenFor(Request.Cookies[GuestCookieName], ticket))
        {
            return NotFound();
        }

        var model = await BuildDisplayAsync(ticket, default, guest: true);
        model.Message = DoneMessage(done);

        return View(nameof(Display), model);
    }

    /// <summary>
    /// Adds a guest's reply, with the cookie their link was traded for.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="body">The reply.</param>
    /// <param name="files">Its files.</param>
    /// <returns>The request, or the page with what's wrong and the reply kept.</returns>
    [AllowAnonymous]
    [HttpPost("requests/{id}/reply", Name = "TicketPortalGuestReply")]
    [RequestSizeLimit(TicketUploads.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketUploads.MaxRequestBytes)]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.PortalGuest)]
    public async Task<IActionResult> GuestReply(string id, string body, List<IFormFile> files)
    {
        if (await _tickets.GetAsync(id) is not { } ticket || !_access.IsGuestTokenFor(Request.Cookies[GuestCookieName], ticket))
        {
            return NotFound();
        }

        // The cookie proves who it is: the requester.
        var part = ticket.GetOrCreate<TicketPart>();
        var (sent, error) = await ReplyAsync(ticket, body, files, part.Requester, part.RequesterContact?.Name ?? part.RequesterContact?.Email);

        if (error is not null)
        {
            var model = await BuildDisplayAsync(ticket, default, guest: true);
            model.Error = error;
            model.Draft = Draft(body);

            return View(nameof(Display), model);
        }

        if (sent.ContentItemId != id)
        {
            // A follow-up of a closed request: the guest follows it with a link of its own.
            KeepToken(GuestCookieName, sent.ContentItemId, _access.CreateGuestToken(sent));
        }

        return RedirectToRoute(GuestRouteName, new { id = sent.ContentItemId, done = Done.Sent });
    }

    /// <summary>
    /// Records the signed-in requester's rating of a solved request.
    /// </summary>
    /// <param name="number">The request's number.</param>
    /// <param name="score">The score.</param>
    /// <param name="comment">What they added.</param>
    /// <returns>The request.</returns>
    [Authorize]
    [HttpPost("account/requests/{number}/rate", Name = "TicketPortalRateOwn")]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.PortalReply)]
    public async Task<IActionResult> RateOwn(string number, int score, string comment)
    {
        var viewer = await _access.GetViewerAsync(User, HttpContext.RequestAborted);
        var ticket = await _tickets.FindByNumberAsync(number);

        // Only the requester rates; an account administrator follows the account's requests but didn't ask for them.
        if (!await _access.CanSeeAsync(viewer, ticket, HttpContext.RequestAborted) || ticket.GetOrCreate<TicketPart>().Requester != viewer.Party)
        {
            return NotFound();
        }

        var error = await RateAsync(ticket, score, comment);

        if (error is not null)
        {
            var model = await BuildDisplayAsync(ticket, viewer.Party, guest: false);
            model.Error = error;

            return View(nameof(Display), model);
        }

        return RedirectToRoute(DisplayRouteName, new { number, done = Done.Rated });
    }

    /// <summary>
    /// The page a requester rates a ticket on, from the signed link in the rating e-mail. The token is traded for a
    /// cookie, and the page reloads without it.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="token">The signed token, on the first visit.</param>
    /// <param name="score">A score chosen in the e-mail, if any.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    [HttpGet("requests/{id}/rate", Name = RateRouteName)]
    public async Task<IActionResult> Rate(string id, string token, int? score = null)
    {
        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(token))
        {
            if (!_access.IsRatingTokenFor(token, ticket))
            {
                return NotFound();
            }

            KeepToken(RatingCookieName, id, token);

            return RedirectToRoute(RateRouteName, new { id, score });
        }

        if (!_access.IsRatingTokenFor(Request.Cookies[RatingCookieName], ticket))
        {
            return NotFound();
        }

        var part = ticket.GetOrCreate<TicketPart>();

        return View(new TicketPortalRateViewModel
        {
            TicketId = id,
            Number = part.Number,
            Subject = ticket.DisplayText,
            Score = score ?? part.Satisfaction?.Score,
            Comment = part.Satisfaction?.Comment,
            Rated = part.Satisfaction is not null,
        });
    }

    /// <summary>
    /// Records a rating, with the cookie the rating link was traded for.
    /// </summary>
    /// <param name="id">The ticket's id.</param>
    /// <param name="score">The score.</param>
    /// <param name="comment">What they added.</param>
    /// <returns>The rating page, saying thanks or what's wrong.</returns>
    [AllowAnonymous]
    [HttpPost("requests/{id}/rate", Name = "TicketPortalRatePost")]
    [ActionName(nameof(Rate))]
    [global::OrchardCore.RateLimits.RateLimitGroup(TicketsConstants.RateLimitGroups.PortalGuest)]
    public async Task<IActionResult> RatePost(string id, int score, string comment)
    {
        if (await _tickets.GetAsync(id) is not { } ticket || !_access.IsRatingTokenFor(Request.Cookies[RatingCookieName], ticket))
        {
            return NotFound();
        }

        var error = await RateAsync(ticket, score, comment);
        var part = (await _tickets.GetAsync(id))?.GetOrCreate<TicketPart>() ?? ticket.GetOrCreate<TicketPart>();

        return View(new TicketPortalRateViewModel
        {
            TicketId = id,
            Number = part.Number,
            Subject = ticket.DisplayText,
            Score = error is null ? part.Satisfaction?.Score : score,
            Comment = error is null ? part.Satisfaction?.Comment : comment,
            Rated = part.Satisfaction is not null,
            Thanked = error is null,
            Error = error,
        });
    }

    // Why the rating wasn't saved, or null.
    private async Task<string> RateAsync(ContentItem ticket, int score, string comment)
    {
        var result = await _tickets.RateAsync(ticket.ContentItemId, score, comment, HttpContext.RequestAborted);

        return result.Succeeded ? null : string.Join(" ", result.Errors);
    }

    // The ticket the reply went on (a follow-up of a closed one, maybe), or why it wasn't sent; nothing is kept then.
    private async Task<(ContentItem Ticket, string Error)> ReplyAsync(ContentItem ticket, string body, List<IFormFile> files, PartyRef author, string authorName)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, H["Write your reply first."].Value);
        }

        if (await RecentRepliesAsync(ticket.ContentItemId) >= MaxRepliesPerHour)
        {
            return (null, H["You sent many replies to this request in the last hour. Please wait a little before sending another."].Value);
        }

        var entryId = UniqueId.GenerateId();
        var (attached, errors) = await _uploads.StoreAsync(files, entryId, author, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            return (null, string.Join(" ", errors));
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

            return (null, string.Join(" ", result.Errors));
        }

        return (result.Ticket ?? ticket, null);
    }

    // The replies sent from the portal on a request in the last hour.
    private async Task<int> RecentRepliesAsync(string ticketId)
    {
        var since = HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow.AddHours(-1);
        var page = await _timeline.ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticketId),
            Kinds = [TicketsConstants.EntryKinds.Reply],
            Take = MaxRepliesPerHour * 2,
        }, HttpContext.RequestAborted);

        return page.Entries.Count(entry => entry.CreatedUtc >= since && entry.Channel == TicketsConstants.Channels.Portal);
    }

    private void KeepToken(string cookieName, string ticketId, string token)
        => Response.Cookies.Append(cookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = Request.PathBase.Add("/requests/" + ticketId).Value,
            MaxAge = TimeSpan.FromHours(12),
        });

    private static string Draft(string body)
        => string.IsNullOrEmpty(body) ? null : body.Length > TimelineConstants.MaxBodyLength ? body[..TimelineConstants.MaxBodyLength] : body;

    private string DoneMessage(string done)
        => done switch
        {
            Done.Created => H["We received your request."].Value,
            Done.Sent => H["Your reply was sent."].Value,
            Done.Rated => H["Thank you for your rating."].Value,
            _ => null,
        };

    // What a page was sent back after, so it says so itself: the site's theme may not show notifications.
    private static class Done
    {
        public const string Created = "created";
        public const string Sent = "sent";
        public const string Rated = "rated";
    }

    private async Task<TicketPortalDisplayViewModel> BuildDisplayAsync(ContentItem ticket, PartyRef viewer, bool guest)
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
            IsGuest = guest,

            // The requester rates a solved request on its page; a guest rates from the link in the rating e-mail.
            CanRate = !guest && !viewer.IsEmpty && part.Requester == viewer && part.StatusCategory is TicketStatusCategory.Solved or TicketStatusCategory.Closed,
            Score = part.Satisfaction?.Score,
            RatingComment = part.Satisfaction?.Comment,
        };

        // Only public replies: internal notes and the record of changes are the business's own. The latest ones, and the
        // original request, which a long thread would otherwise drop.
        var query = new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
            Kinds = [TicketsConstants.EntryKinds.Reply],
            IncludeInternal = false,
            Take = ThreadTake,
        };
        var page = await _timeline.ListAsync(query, HttpContext.RequestAborted);
        var entries = page.Entries.ToList();

        if (page.Total > entries.Count)
        {
            query.Skip = page.Total - 1;
            query.Take = 1;
            entries.AddRange((await _timeline.ListAsync(query, HttpContext.RequestAborted)).Entries);
            model.Skipped = page.Total - entries.Count;
        }

        var attachments = _attachments.FirstOrDefault();

        foreach (var entry in entries.Where(entry => entry.Visibility == TimelineVisibility.Public && entry.RedactedUtc is null).OrderBy(entry => entry.CreatedUtc))
        {
            // The requester's side: their messages, a guest's (which had no author before), and a colleague's on an
            // account's request. "You" is only what the viewer wrote.
            var fromRequesterSide = entry.Author.IsEmpty || entry.Author == part.Requester || entry.Channel == TicketsConstants.Channels.Portal;
            var row = new TicketPortalEntry
            {
                AuthorName = entry.AuthorName,
                IsRequester = fromRequesterSide,
                IsMine = guest
                    ? fromRequesterSide
                    : !viewer.IsEmpty && (entry.Author == viewer || (entry.Author.IsEmpty && part.Requester == viewer)),
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

    private async Task<bool> PrepareCreateAsync(TicketPortalCreateViewModel model, TicketPortalViewer viewer)
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

        var desks = await CustomerDesksAsync(viewer);
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

    // The desks a requester may open requests on: those that serve customers and, for a member of staff (with Staff on),
    // the internal ones, such as IT and HR.
    private async Task<List<TicketDesk>> CustomerDesksAsync(TicketPortalViewer viewer)
        => [.. (await _desks.GetAllAsync()).Where(desk => TicketPortalAccess.Offers(desk, viewer?.IsStaff == true))];

    private static string StatusLabel(Dictionary<string, TicketStatus> statuses, TicketPart part)
        => part.StatusId is not null && statuses.TryGetValue(part.StatusId, out var status)
            ? string.IsNullOrWhiteSpace(status.CustomerLabel) ? status.Name : status.CustomerLabel
            : null;

    private async Task<string> LocalAsync(DateTime? utc)
        => utc is { } value
            ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture)
            : null;
}

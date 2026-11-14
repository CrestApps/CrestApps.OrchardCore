using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// The agent pages of tickets: the list, a new ticket on behalf of a requester, the ticket page with its thread and
/// composer, and the changes made from it. Every change goes through <see cref="ITicketService"/>, and every page and
/// action checks the user's desk permissions and view scope through <see cref="ITicketAccessService"/>.
/// </summary>
[Admin]
public sealed class TicketsController : Controller
{
    private const string DraftKey = "TicketReplyDraft";
    private const int PreviousTicketsShown = 5;

    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketView> _views;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IPartyDirectory _parties;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly PagerOptions _pagerOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsController"/> class.
    /// </summary>
    /// <param name="tickets">The ticket service.</param>
    /// <param name="access">The ticket access service.</param>
    /// <param name="statuses">The statuses.</param>
    /// <param name="priorities">The priorities.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="views">The saved views.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentItemDisplayManager">The content item display manager.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="parties">The party directory.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="serviceProvider">The service provider, for optional services.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketsController(
        ITicketService tickets,
        ITicketAccessService access,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        ICatalog<TicketDesk> desks,
        ICatalog<TicketView> views,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IPartyDirectory parties,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IClock clock,
        IAuthorizationService authorizationService,
        IServiceProvider serviceProvider,
        IOptions<PagerOptions> pagerOptions,
        IHtmlLocalizer<TicketsController> htmlLocalizer,
        IStringLocalizer<TicketsController> stringLocalizer)
    {
        _tickets = tickets;
        _access = access;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _views = views;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _parties = parties;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _clock = clock;
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
        _pagerOptions = pagerOptions.Value;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the tickets the user may view, from a saved view or the pickers.
    /// </summary>
    /// <param name="form">The search and pickers.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>The page.</returns>
    [Admin("tickets", "Tickets")]
    public async Task<IActionResult> Index(TicketsListViewModel form, PagerParameters pagerParameters)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var scopes = await _access.GetViewScopesAsync(User, cancellationToken);

        if (scopes.Count == 0)
        {
            return Forbid();
        }

        var views = await VisibleViewsAsync(cancellationToken);
        var view = string.IsNullOrEmpty(form.ViewId) ? null : views.FirstOrDefault(item => item.ItemId == form.ViewId);
        var query = TicketListQueries.Build(form, view);
        var pager = new Pager(pagerParameters, _pagerOptions);

        query.Skip = pager.GetStartIndex();
        query.Take = pager.PageSize;

        var (tickets, total) = await _tickets.ListAsync(await _access.ApplyScopeAsync(User, query, cancellationToken), cancellationToken);
        var lookups = await LookupsAsync(cancellationToken);

        form.Options = new TicketsListOptions
        {
            Views = [.. views.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.ViewId))],
            Statuses = [.. lookups.Statuses.Values.OrderBy(item => item.Order).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.StatusId))],
            Desks = [.. lookups.Desks.Values.Where(desk => scopes.ContainsKey(desk.ItemId)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.DeskId))],
            Priorities = [.. lookups.Priorities.Values.OrderByDescending(item => item.Rank).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.PriorityId))],
            Teams = [.. (await TeamOptionsAsync(cancellationToken)).Select(item => new SelectListItem(item.Text, item.Value, item.Value == form.TeamId))],
            Types = [.. (await TicketTypesAsync()).Select(item => new SelectListItem(item.Text, item.Value, item.Value == form.ContentType))],
        };

        form.BulkActions = [new SelectListItem(S["Assign to me"], BulkActions.AssignToMe)];
        form.BulkActions.AddRange(form.Options.Statuses.Select(status => new SelectListItem(S["Set status: {0}", status.Text], BulkActions.StatusPrefix + status.Value)));
        form.BulkActions.Add(new SelectListItem(S["Merge into the oldest"], BulkActions.Merge));
        form.BulkActions.Add(new SelectListItem(S["Delete"], BulkActions.Delete));

        form.List = new AdminListContext<TicketRow>
        {
            Source = await RowsAsync(tickets, lookups, cancellationToken),
            Pager = await _shapeFactory.PagerAsync(pager, total, PagerRouteData()),
        };

        return View(form);
    }

    /// <summary>
    /// Applies a bulk action to the chosen tickets: assign to me, set a status, merge into the oldest, or delete. Each
    /// ticket is checked on its own; the ones the user may not change are left as they were.
    /// </summary>
    /// <param name="bulkAction">The action.</param>
    /// <param name="itemIds">The chosen tickets.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("tickets/bulk", "TicketsBulk")]
    public async Task<IActionResult> Bulk(string bulkAction, string[] itemIds, string returnUrl)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var tickets = new List<ContentItem>();

        foreach (var id in itemIds?.Distinct(StringComparer.Ordinal) ?? [])
        {
            if (await _tickets.GetAsync(id) is { } ticket && await _access.CanAsync(User, ticket, TicketOperation.ViewAssigned, cancellationToken))
            {
                tickets.Add(ticket);
            }
        }

        if (tickets.Count == 0 || string.IsNullOrEmpty(bulkAction))
        {
            await _notifier.WarningAsync(H["Choose the tickets and an action first."]);

            return Back(returnUrl);
        }

        var done = 0;
        var refused = 0;

        if (bulkAction == BulkActions.Merge)
        {
            var oldest = tickets.OrderBy(ticket => ticket.CreatedUtc).First();

            foreach (var ticket in tickets.Where(item => item != oldest))
            {
                var allowed = await _access.CanAsync(User, ticket, TicketOperation.Merge, cancellationToken)
                    && await _access.CanAsync(User, oldest, TicketOperation.Merge, cancellationToken);

                Count(allowed && (await _tickets.MergeAsync(ticket.ContentItemId, oldest.ContentItemId, Actor(), cancellationToken)).Succeeded);
            }
        }
        else
        {
            foreach (var ticket in tickets)
            {
                var succeeded = bulkAction switch
                {
                    BulkActions.AssignToMe => await _access.CanAsync(User, ticket, TicketOperation.Assign, cancellationToken)
                        && (await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { AssigneeId = UserId() }, Actor(), cancellationToken)).Succeeded,
                    BulkActions.Delete => await _access.CanAsync(User, ticket, TicketOperation.Delete, cancellationToken)
                        && (await _tickets.DeleteAsync(ticket.ContentItemId, cancellationToken)).Succeeded,
                    _ when bulkAction.StartsWith(BulkActions.StatusPrefix, StringComparison.Ordinal) => await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken)
                        && (await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { StatusId = bulkAction[BulkActions.StatusPrefix.Length..] }, Actor(), cancellationToken)).Succeeded,
                    _ => false,
                };

                Count(succeeded);
            }
        }

        if (done > 0)
        {
            await _notifier.SuccessAsync(H["{0} tickets changed.", done]);
        }

        if (refused > 0)
        {
            await _notifier.WarningAsync(H["{0} tickets were left as they were: you may not change them, or they can't change.", refused]);
        }

        return Back(returnUrl);

        void Count(bool succeeded)
        {
            if (succeeded)
            {
                done++;
            }
            else
            {
                refused++;
            }
        }
    }

    /// <summary>
    /// Shows the new-ticket page for a ticket type.
    /// </summary>
    /// <param name="contentType">The ticket type, or none for the default one.</param>
    /// <param name="requester">The requester, as <c>Kind:Id</c>, when started from a contact or a user.</param>
    /// <param name="accountId">The account, when started from an account.</param>
    /// <returns>The page.</returns>
    [Admin("tickets/new/{contentType?}", "TicketsCreate")]
    public async Task<IActionResult> Create(string contentType = null, string requester = null, string accountId = null)
    {
        var model = new TicketCreateViewModel { ContentType = contentType, Requester = requester, AccountId = accountId };

        if (!await PrepareCreateAsync(model))
        {
            return model.Desks.Count == 0 ? Forbid() : NotFound();
        }

        var ticket = await _contentManager.NewAsync(model.ContentType);
        model.Editor = await _contentItemDisplayManager.BuildEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(model);
    }

    /// <summary>
    /// Creates a ticket on behalf of a requester.
    /// </summary>
    /// <param name="model">The ticket's values.</param>
    /// <param name="files">The files of the first message.</param>
    /// <returns>The new ticket's page, or the form with what's wrong.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("tickets/new/{contentType?}", "TicketsCreate")]
    public async Task<IActionResult> CreatePost(TicketCreateViewModel model, List<IFormFile> files)
    {
        if (!await PrepareCreateAsync(model))
        {
            return model.Desks.Count == 0 ? Forbid() : NotFound();
        }

        var cancellationToken = HttpContext.RequestAborted;

        if (!model.Desks.Any(desk => desk.Value == model.DeskId))
        {
            ModelState.AddModelError(nameof(model.DeskId), S["Choose a desk you may create tickets in."]);
        }

        var ticket = await _contentManager.NewAsync(model.ContentType);
        model.Editor = await _contentItemDisplayManager.UpdateEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!string.IsNullOrWhiteSpace(model.Subject))
        {
            ticket.DisplayText = model.Subject.Trim();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!PartyRef.TryParse(model.Requester, out var requester))
        {
            requester = default;
        }
        var entryId = UniqueId.GenerateId();
        var (attached, uploadError) = await StoreFilesAsync(files, entryId, cancellationToken);

        if (uploadError)
        {
            return View(model);
        }

        var result = await _tickets.CreateAsync(new TicketCreateRequest
        {
            Ticket = ticket,
            ContentType = model.ContentType,
            Subject = model.Subject,
            Body = model.Body,
            EntryId = entryId,
            AttachmentIds = attached,
            DeskId = model.DeskId,
            PriorityId = model.PriorityId,
            Requester = requester,
            RequesterContact = requester.IsEmpty && (!string.IsNullOrWhiteSpace(model.RequesterName) || !string.IsNullOrWhiteSpace(model.RequesterEmail))
                ? new ContactInfo { Name = model.RequesterName?.Trim(), Email = model.RequesterEmail?.Trim() }
                : null,
            AccountId = model.AccountId,
            Channel = TicketsConstants.Channels.Agent,
            CreatedBy = CurrentParty(),
        }, cancellationToken);

        if (!result.Succeeded)
        {
            await DeleteFilesAsync(attached);

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        await _notifier.SuccessAsync(H["Ticket {0} was created.", result.Ticket.GetOrCreate<TicketPart>().Number]);

        return RedirectToAction(nameof(Display), new { id = result.Ticket.ContentItemId });
    }

    /// <summary>
    /// Shows a ticket: its thread and composer beside the panels the display drivers add.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The page.</returns>
    [Admin("tickets/{id}", "TicketsDisplay")]
    public async Task<IActionResult> Display(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return Forbid();
        }

        var part = ticket.GetOrCreate<TicketPart>();
        var statuses = await _statuses.GetAllAsync(cancellationToken);
        var readOnly = TicketLifecycle.IsReadOnly(part);

        var model = new TicketPageViewModel
        {
            Ticket = ticket,
            Part = part,
            Status = statuses.FirstOrDefault(status => status.ItemId == part.StatusId),
            Priority = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken),
            Display = await _contentItemDisplayManager.BuildDisplayAsync(ticket, _updateModelAccessor.ModelUpdater, TicketsConstants.DisplayTypes.DetailAdmin),
            Statuses = [.. statuses
                .Where(status => status.Category is not (TicketStatusCategory.New or TicketStatusCategory.Closed))
                .OrderBy(status => status.Order)
                .Select(status => new SelectListItem(status.Name, status.ItemId))],
            CanReply = !readOnly && await _access.CanAsync(User, ticket, TicketOperation.Reply, cancellationToken),
            CanNote = !readOnly && await _access.CanAsync(User, ticket, TicketOperation.Note, cancellationToken),
            CanEdit = !readOnly && await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken),
            CanMerge = !readOnly && await _access.CanAsync(User, ticket, TicketOperation.Merge, cancellationToken),
            CanDelete = await _access.CanAsync(User, ticket, TicketOperation.Delete, cancellationToken),
            CanAttach = _serviceProvider.GetService<IAttachmentService>() is not null,
            IsReadOnly = readOnly,
            Draft = TempData?.Peek(DraftKey) is string draft && draft.StartsWith(id + "\n", StringComparison.Ordinal) ? PopDraft(id) : null,
            MergedInto = string.IsNullOrEmpty(part.MergedIntoId) ? null : await _tickets.GetAsync(part.MergedIntoId),
            FollowUpOf = string.IsNullOrEmpty(part.FollowUpOfId) ? null : await _tickets.GetAsync(part.FollowUpOfId),
        };

        return View(model);
    }

    /// <summary>
    /// Adds a reply to the requester or an internal note, and optionally sets the status in the same step.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="body">The text.</param>
    /// <param name="isInternal">Whether it's an internal note.</param>
    /// <param name="setStatusId">The status to set with it, if any.</param>
    /// <param name="files">The files to attach.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("tickets/{id}/reply", "TicketsReply")]
    public async Task<IActionResult> Reply(string id, string body, bool isInternal, string setStatusId, List<IFormFile> files)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, isInternal ? TicketOperation.Note : TicketOperation.Reply, cancellationToken))
        {
            return Forbid();
        }

        var entryId = UniqueId.GenerateId();
        var (attached, uploadError) = await StoreFilesAsync(files, entryId, cancellationToken);

        if (uploadError)
        {
            return KeepDraft(id, body);
        }

        var result = await _tickets.ReplyAsync(id, new TicketReply
        {
            Body = body,
            IsInternal = isInternal,
            Author = CurrentParty(),
            AuthorName = User.Identity?.Name,
            Channel = TicketsConstants.Channels.Agent,
            EntryId = entryId,
            AttachmentIds = attached,
            SetStatusId = string.IsNullOrEmpty(setStatusId) ? null : setStatusId,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            await DeleteFilesAsync(attached);

            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return KeepDraft(id, body);
        }

        await _notifier.SuccessAsync(isInternal ? H["The note was added."] : H["The reply was sent."]);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Changes the ticket's values from the properties panel: status, priority and desk need the edit permission, team
    /// and assignee the assign permission.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="update">The values.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("tickets/{id}/update", "TicketsUpdate")]
    public async Task<IActionResult> Update(string id, TicketUpdate update)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (update is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        var part = ticket.GetOrCreate<TicketPart>();
        var canEdit = await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken);
        var canAssign = await _access.CanAsync(User, ticket, TicketOperation.Assign, cancellationToken);

        if (!canEdit && !canAssign)
        {
            return Forbid();
        }

        // A value the user may not change, or didn't change, is left alone.
        var change = new TicketUpdate
        {
            StatusId = canEdit && Changed(update.StatusId, part.StatusId) ? update.StatusId : null,
            PriorityId = canEdit && Changed(update.PriorityId, part.PriorityId) ? update.PriorityId : null,
            DeskId = canEdit && Changed(update.DeskId, part.DeskId) ? update.DeskId : null,
            TeamId = canAssign && (update.TeamId ?? string.Empty) != (part.TeamId ?? string.Empty) ? update.TeamId ?? string.Empty : null,
            AssigneeId = canAssign && (update.AssigneeId ?? string.Empty) != (part.AssigneeId ?? string.Empty) ? update.AssigneeId ?? string.Empty : null,
        };

        if (change.DeskId is not null && await _desks.FindByIdAsync(change.DeskId, cancellationToken) is { } newDesk
            && !(await _access.GetViewScopesAsync(User, cancellationToken)).ContainsKey(newDesk.ItemId))
        {
            await _notifier.ErrorAsync(H["You may not move tickets to the {0} desk.", newDesk.Name]);

            return RedirectToAction(nameof(Display), new { id });
        }

        var result = await _tickets.UpdateAsync(id, change, Actor(), cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(H["The ticket was saved."]);
        }

        // The user may no longer see a ticket they gave away.
        return result.Succeeded && !await _access.CanAsync(User, result.Ticket, TicketOperation.ViewAssigned, cancellationToken)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Assigns the ticket to the user.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("tickets/{id}/take", "TicketsTake")]
    public async Task<IActionResult> Take(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Assign, cancellationToken))
        {
            return Forbid();
        }

        await NotifyAsync(await _tickets.UpdateAsync(id, new TicketUpdate { AssigneeId = UserId() }, Actor(), cancellationToken), H["The ticket is yours."]);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Shows the editor of the ticket type's fields.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The page.</returns>
    [Admin("tickets/{id}/edit", "TicketsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        ViewData["Ticket"] = ticket;

        return View(await _contentItemDisplayManager.BuildEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: false));
    }

    /// <summary>
    /// Saves the ticket type's fields.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The ticket page, or the editor with what's wrong.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("tickets/{id}/edit", "TicketsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken))
        {
            return Forbid();
        }

        IShape editor = null;
        var result = await _tickets.EditAsync(id, async item =>
        {
            editor = await _contentItemDisplayManager.UpdateEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: false);

            return ModelState.IsValid;
        }, Actor(), cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            ViewData["Ticket"] = result.Ticket ?? ticket;

            return View(editor ?? await _contentItemDisplayManager.BuildEditorAsync(ticket, _updateModelAccessor.ModelUpdater, isNew: false));
        }

        await _notifier.SuccessAsync(H["The ticket was saved."]);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Links the ticket to a record, or removes a link.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="record">The record, as <c>Kind:Id</c>.</param>
    /// <param name="kind">How they relate; empty removes the link.</param>
    /// <param name="recordKind">The kind chosen in the links panel, whose picker (<c>record-{kind}</c>) holds the record.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("tickets/{id}/link", "TicketsLink")]
    public async Task<IActionResult> Link(string id, string record, string kind, string recordKind)
    {
        if (string.IsNullOrEmpty(record) && !string.IsNullOrEmpty(recordKind) && Request.HasFormContentType)
        {
            record = Request.Form["record-" + recordKind].FirstOrDefault(value => !string.IsNullOrEmpty(value));
        }

        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken))
        {
            return Forbid();
        }

        if (!RecordRef.TryParse(record, out var target))
        {
            await _notifier.ErrorAsync(H["Choose a record to link."]);

            return RedirectToAction(nameof(Display), new { id });
        }

        await NotifyAsync(await _tickets.LinkAsync(id, target, kind, Actor(), cancellationToken), string.IsNullOrEmpty(kind) ? H["The link was removed."] : H["The link was saved."]);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Merges the ticket into another one, chosen by its number.
    /// </summary>
    /// <param name="id">The ticket that goes away.</param>
    /// <param name="intoNumber">The number of the ticket it goes into.</param>
    /// <returns>The page of the ticket it went into.</returns>
    [HttpPost]
    [Admin("tickets/{id}/merge", "TicketsMerge")]
    public async Task<IActionResult> Merge(string id, string intoNumber)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        var into = await _tickets.FindByNumberAsync(intoNumber);

        if (!await _access.CanAsync(User, ticket, TicketOperation.Merge, cancellationToken))
        {
            return Forbid();
        }

        if (into is null || !await _access.CanAsync(User, into, TicketOperation.Merge, cancellationToken))
        {
            await _notifier.ErrorAsync(H["No ticket you may merge into is numbered {0}.", intoNumber ?? string.Empty]);

            return RedirectToAction(nameof(Display), new { id });
        }

        var result = await _tickets.MergeAsync(id, into.ContentItemId, Actor(), cancellationToken);
        await NotifyAsync(result, H["The ticket was merged into {0}.", into.GetOrCreate<TicketPart>().Number]);

        return RedirectToAction(nameof(Display), new { id = result.Succeeded ? into.ContentItemId : id });
    }

    /// <summary>
    /// Deletes the ticket.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("tickets/{id}/delete", "TicketsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Delete, cancellationToken))
        {
            return Forbid();
        }

        await NotifyAsync(await _tickets.DeleteAsync(id, cancellationToken), H["Ticket {0} was deleted.", ticket.GetOrCreate<TicketPart>().Number]);

        return RedirectToAction(nameof(Index));
    }

    // Fills the new-ticket form's choices; false when the type isn't a ticket type or the user may create in no desk.
    private async Task<bool> PrepareCreateAsync(TicketCreateViewModel model)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var desks = new List<TicketDesk>();

        foreach (var desk in (await _desks.GetAllAsync(cancellationToken)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            // Creating a ticket for someone is replying on a desk.
            if (await _authorizationService.AuthorizeAsync(User, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
            {
                desks.Add(desk);
            }
        }

        var types = await TicketTypesAsync();

        model.ContentType = string.IsNullOrEmpty(model.ContentType) ? types.FirstOrDefault()?.Value : model.ContentType;
        model.Types = [.. types.Select(type => new SelectListItem(type.Text, type.Value, type.Value == model.ContentType))];

        var offered = desks.Where(desk => desk.TicketTypes.Count == 0 || desk.TicketTypes.Contains(model.ContentType ?? string.Empty, StringComparer.OrdinalIgnoreCase)).ToList();

        model.DeskId ??= offered.FirstOrDefault()?.ItemId;
        model.Desks = [.. offered.Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == model.DeskId))];
        model.Priorities = [.. (await _priorities.GetAllAsync(cancellationToken)).OrderByDescending(item => item.Rank).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.PriorityId))];
        model.CanAttach = _serviceProvider.GetService<IAttachmentService>() is not null;

        return desks.Count > 0 && types.Any(type => type.Value == model.ContentType);
    }

    // The saved views the user may use: shared with everyone, owned by the user, or shared with one of the user's teams.
    private async Task<List<TicketView>> VisibleViewsAsync(CancellationToken cancellationToken)
    {
        var userId = UserId();
        var teams = _serviceProvider.GetService<ITeamMembershipService>() is { } memberships && !string.IsNullOrEmpty(userId)
            ? (await memberships.GetMembershipsAsync(userId, cancellationToken: cancellationToken)).Select(member => member.TeamId).ToHashSet(StringComparer.Ordinal)
            : [];

        return [.. (await _views.GetAllAsync(cancellationToken))
            .Where(view => (string.IsNullOrEmpty(view.OwnerId) && string.IsNullOrEmpty(view.SharedWithTeamId))
                || view.OwnerId == userId
                || (!string.IsNullOrEmpty(view.SharedWithTeamId) && teams.Contains(view.SharedWithTeamId)))
            .OrderBy(view => view.Order)
            .ThenBy(view => view.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private async Task<List<SelectListItem>> TicketTypesAsync()
        => [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            .OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(type => new SelectListItem(type.DisplayName, type.Name))];

    private async Task<List<SelectListItem>> TeamOptionsAsync(CancellationToken cancellationToken)
        => _serviceProvider.GetService<ITeamManager>() is { } teams
            ? [.. (await teams.ListTreeAsync(cancellationToken)).Select(item => new SelectListItem(new string(' ', item.Depth * 2) + item.Team.Name, item.Team.ItemId))]
            : [];

    private async Task<(Dictionary<string, TicketStatus> Statuses, Dictionary<string, TicketPriority> Priorities, Dictionary<string, TicketDesk> Desks)> LookupsAsync(CancellationToken cancellationToken)
        => (
            (await _statuses.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal),
            (await _priorities.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal),
            (await _desks.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal));

    private async Task<List<TicketRow>> RowsAsync(
        IReadOnlyList<ContentItem> tickets,
        (Dictionary<string, TicketStatus> Statuses, Dictionary<string, TicketPriority> Priorities, Dictionary<string, TicketDesk> Desks) lookups,
        CancellationToken cancellationToken)
    {
        var parts = tickets.Select(ticket => (Ticket: ticket, Part: ticket.GetOrCreate<TicketPart>() ?? new TicketPart())).ToList();
        var parties = parts
            .SelectMany(item => new[] { item.Part.Requester, string.IsNullOrEmpty(item.Part.AssigneeId) ? default : new PartyRef(PartyKinds.User, item.Part.AssigneeId) })
            .Where(party => !party.IsEmpty)
            .Distinct()
            .ToList();
        var names = await _parties.GetNamesAsync(parties, cancellationToken);
        var teamNames = (await TeamOptionsAsync(cancellationToken)).ToDictionary(item => item.Value, item => item.Text.Trim(' '), StringComparer.Ordinal);
        var now = _clock.UtcNow;

        return [.. parts.Select(item => new TicketRow
        {
            Ticket = item.Ticket,
            Part = item.Part,
            Status = lookups.Statuses.GetValueOrDefault(item.Part.StatusId ?? string.Empty),
            Priority = lookups.Priorities.GetValueOrDefault(item.Part.PriorityId ?? string.Empty),
            DeskName = lookups.Desks.GetValueOrDefault(item.Part.DeskId ?? string.Empty)?.Name,
            TeamName = teamNames.GetValueOrDefault(item.Part.TeamId ?? string.Empty),
            AssigneeName = string.IsNullOrEmpty(item.Part.AssigneeId) ? null : names.GetValueOrDefault(new PartyRef(PartyKinds.User, item.Part.AssigneeId), item.Part.AssigneeId),
            RequesterName = !item.Part.Requester.IsEmpty && names.TryGetValue(item.Part.Requester, out var requesterName)
                ? requesterName
                : item.Part.RequesterContact?.Name ?? item.Part.RequesterContact?.Email,
            NowUtc = now,
        })];
    }

    // Stores the uploaded files as files of the entry that is about to be added; true in the second value when a file
    // was refused (the notifier says why).
    private async Task<(List<string> Attached, bool Refused)> StoreFilesAsync(List<IFormFile> files, string entryId, CancellationToken cancellationToken)
    {
        var uploads = files?.Where(file => file is { Length: > 0 }).ToList() ?? [];

        if (uploads.Count == 0)
        {
            return ([], false);
        }

        if (_serviceProvider.GetService<IAttachmentService>() is not { } attachments)
        {
            await _notifier.ErrorAsync(H["Files can't be attached here."]);

            return ([], true);
        }

        var owner = new RecordRef(TimelineConstants.AttachmentOwnerKind, entryId);
        var read = new List<AttachmentUpload>(uploads.Count);

        foreach (var file in uploads)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);

            read.Add(new AttachmentUpload
            {
                Owner = owner,
                FileName = file.FileName,
                DeclaredContentType = file.ContentType,
                Content = buffer.ToArray(),
                UploadedBy = CurrentParty(),
            });
        }

        var stored = await attachments.AddAsync(read, cancellationToken);

        if (!stored.Succeeded)
        {
            foreach (var error in stored.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return ([], true);
        }

        return ([.. stored.Attachments.Select(attachment => attachment.ItemId)], false);
    }

    private async Task DeleteFilesAsync(List<string> attachmentIds)
    {
        if (attachmentIds.Count == 0 || _serviceProvider.GetService<IAttachmentService>() is not { } attachments)
        {
            return;
        }

        foreach (var attachmentId in attachmentIds)
        {
            await attachments.DeleteAsync(attachmentId, CancellationToken.None);
        }
    }

    private async Task NotifyAsync(TicketResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }

    private RedirectToActionResult KeepDraft(string id, string body)
    {
        if (!string.IsNullOrEmpty(body))
        {
            TempData[DraftKey] = id + "\n" + body;
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    private string PopDraft(string id)
    {
        var draft = (string)TempData[DraftKey];

        return draft[(id.Length + 1)..];
    }

    // The list's search and pickers, kept on the pager's links.
    private RouteData PagerRouteData()
    {
        var routeData = new RouteData();

        foreach (var (key, value) in Request.Query)
        {
            if (!string.Equals(key, "page", StringComparison.OrdinalIgnoreCase) && !string.Equals(key, "pageSize", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
            {
                routeData.Values[key] = value.ToString();
            }
        }

        return routeData;
    }

    private IActionResult Back(string returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));

    private static bool Changed(string value, string current)
        => !string.IsNullOrEmpty(value) && !string.Equals(value, current, StringComparison.Ordinal);

    private string UserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private PartyRef CurrentParty()
        => string.IsNullOrEmpty(UserId()) ? default : new PartyRef(PartyKinds.User, UserId());

    private TicketActor Actor()
        => TicketActor.User(UserId(), User.Identity?.Name);

    /// <summary>
    /// The values of the list's bulk actions.
    /// </summary>
    internal static class BulkActions
    {
        /// <summary>
        /// Assign the tickets to the user.
        /// </summary>
        public const string AssignToMe = "assign-to-me";

        /// <summary>
        /// The start of "set status" actions, followed by the status id.
        /// </summary>
        public const string StatusPrefix = "status:";

        /// <summary>
        /// Merge the tickets into the oldest of them.
        /// </summary>
        public const string Merge = "merge";

        /// <summary>
        /// Delete the tickets.
        /// </summary>
        public const string Delete = "delete";
    }
}

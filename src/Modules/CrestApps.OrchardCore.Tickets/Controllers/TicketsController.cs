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
using CrestApps.OrchardCore.Tickets.Categories;
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
using OrchardCore.Contents;
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
    private const int MaxBulkTickets = 100;

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

        // The account filter names its account to whoever may see it; the tickets are scoped to the user's desks either way.
        if (!string.IsNullOrEmpty(form.AccountId)
            && await _contentManager.GetAsync(form.AccountId, VersionOptions.Latest) is { } account
            && await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, account))
        {
            form.AccountName = account.DisplayText;
        }

        form.Options = new TicketsListOptions
        {
            Views = [.. views.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.ViewId))],
            Statuses = [.. lookups.Statuses.Values.OrderBy(item => item.Order).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.StatusId))],
            Desks = [.. lookups.Desks.Values.Where(desk => scopes.ContainsKey(desk.ItemId)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.DeskId))],
            Priorities = [.. lookups.Priorities.Values.OrderByDescending(item => item.Rank).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == form.PriorityId))],
            Teams = [.. (await TeamOptionsAsync(cancellationToken)).Select(item => new SelectListItem(item.Text, item.Value, item.Value == form.TeamId))],
            Types = [.. (await TicketTypesAsync()).Select(item => new SelectListItem(item.Text, item.Value, item.Value == form.ContentType))],
            Categories = await CategoryFilterOptionsAsync(form.CategoryId, cancellationToken),
        };

        foreach (var desk in lookups.Desks.Values)
        {
            // Opening a ticket for someone is replying on a desk.
            if (await _authorizationService.AuthorizeAsync(User, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
            {
                form.CanCreate = true;

                break;
            }
        }

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
        var chosen = itemIds?.Distinct(StringComparer.Ordinal).ToArray() ?? [];

        // Each changed ticket runs its rules (workflows, e-mails) in this request, so one action takes a bounded number.
        if (chosen.Length > MaxBulkTickets)
        {
            await _notifier.WarningAsync(H["Choose at most {0} tickets at a time.", MaxBulkTickets]);

            return Back(returnUrl);
        }

        foreach (var id in chosen)
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
            await _notifier.SuccessAsync(H.Plural(done, "1 ticket changed.", "{0} tickets changed."));
        }

        if (refused > 0)
        {
            await _notifier.WarningAsync(H.Plural(refused, "1 ticket was left as it was: you may not change it, or it can't change.", "{0} tickets were left as they were: you may not change them, or they can't change."));
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
    public async Task<IActionResult> Create(string contentType = null, string requester = null, string accountId = null, string conversation = null)
    {
        var model = new TicketCreateViewModel { ContentType = contentType, Requester = requester, AccountId = accountId };

        if (!await PrepareCreateAsync(model))
        {
            return model.Desks.Count == 0 ? Forbid() : NotFound();
        }

        var ticket = await _contentManager.NewAsync(model.ContentType);

        // Started from a messaging conversation: the customer, a subject and the latest messages are filled in.
        if (!string.IsNullOrEmpty(conversation)
            && _serviceProvider.GetService<Messaging.TicketConversationSource>() is { } conversations
            && await conversations.GetAsync(User, conversation, HttpContext.RequestAborted) is { } seed)
        {
            model.ConversationId = seed.ConversationId;
            model.Body = seed.Body;

            if (!seed.Requester.IsEmpty)
            {
                model.Requester = seed.Requester.ToString();
            }
            else
            {
                model.RequesterName = seed.CustomerName;
            }

            ticket.DisplayText = seed.Subject;
            ticket.Alter<global::OrchardCore.Title.Models.TitlePart>(title => title.Title = seed.Subject);
        }

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
        var ticket = await _contentManager.NewAsync(model.ContentType);

        // The category goes on the item before its editor is read, so the type's field rules on the category apply.
        var category = await TicketCategoryForm.ApplyAsync(_serviceProvider.GetService<ITicketCategoryService>(), ticket, model.CategoryId, customersOnly: false);

        if (!string.IsNullOrWhiteSpace(model.CategoryId) && category is null)
        {
            ModelState.AddModelError(nameof(model.CategoryId), S["Choose one of this ticket type's categories."]);
        }
        else if (category is null && model.CategoryRequired)
        {
            ModelState.AddModelError(nameof(model.CategoryId), S["Choose a category."]);
        }

        // A desk left to the category: the category's, then the ticket type's, then the first the user may use.
        if (string.IsNullOrEmpty(model.DeskId))
        {
            var settings = await TypeSettingsAsync(model.ContentType);

            model.DeskId = new[] { category?.DeskId, settings.DefaultDeskId }.FirstOrDefault(id => model.Desks.Any(desk => desk.Value == id))
                ?? model.Desks.FirstOrDefault()?.Value;
        }

        if (!model.Desks.Any(desk => desk.Value == model.DeskId))
        {
            ModelState.AddModelError(nameof(model.DeskId), S["Choose a desk you may create tickets in."]);
        }

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

        // A requester is only taken from a kind of party the user may search (as the party picker allows), so a posted id
        // can't be used to read someone's contact details; an account only if the user may view it.
        if (!requester.IsEmpty
            && !(_serviceProvider.GetService<IOptions<PartySourceOptions>>()?.Value.Kinds.TryGetValue(requester.Kind, out var descriptor) == true
                && descriptor.SearchPermission is not null
                && await _authorizationService.AuthorizeAsync(User, descriptor.SearchPermission)))
        {
            ModelState.AddModelError(nameof(model.Requester), S["You may not choose that requester."]);

            return View(model);
        }

        if (!string.IsNullOrEmpty(model.AccountId)
            && (await _contentManager.GetAsync(model.AccountId, VersionOptions.Latest) is not { } account
                || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, account)))
        {
            ModelState.AddModelError(nameof(model.AccountId), S["You may not open a ticket for that account."]);

            return View(model);
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
            CategoryId = category?.Id,
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

        // A ticket that follows up a conversation links to it, once the agent may still read it.
        if (!string.IsNullOrEmpty(model.ConversationId)
            && _serviceProvider.GetService<Messaging.TicketConversationSource>() is { } conversations
            && await conversations.GetAsync(User, model.ConversationId, cancellationToken) is not null)
        {
            await _tickets.LinkAsync(result.Ticket.ContentItemId, new RecordRef(Messaging.TicketConversationSource.RecordKind, model.ConversationId), TicketLinkKinds.Related, Actor(), cancellationToken);
        }

        await _notifier.SuccessAsync(H["Ticket {0} was created.", result.Ticket.GetOrCreate<TicketPart>().Number]);

        return RedirectToAction(nameof(Display), new { id = result.Ticket.ContentItemId });
    }

    /// <summary>
    /// Shows a ticket: its thread and composer beside the panels the display drivers add.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The page.</returns>
    [Admin("ticket/{id}", "TicketsDisplay")]
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
            MergedInto = string.IsNullOrEmpty(part.MergedIntoId) ? null : await _tickets.GetAsync(part.MergedIntoId),
            Actions = !readOnly && await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken) && _serviceProvider.GetService<ITicketActionSource>() is { } actions
                ? await actions.ListAsync(ticket, cancellationToken)
                : [],
            ReplyTemplates = !readOnly && _serviceProvider.GetService<ITicketReplyTemplateSource>() is { } replyTemplates
                ? await replyTemplates.ListAsync(ticket, cancellationToken)
                : [],
            FollowUpOf = string.IsNullOrEmpty(part.FollowUpOfId) ? null : await _tickets.GetAsync(part.FollowUpOfId),
        };

        // A ticket this one points to is named only to people who may see it.
        model.MergedIntoVisible = model.MergedInto is not null && await _access.CanAsync(User, model.MergedInto, TicketOperation.ViewAssigned, cancellationToken);
        model.FollowUpOfVisible = model.FollowUpOf is not null && await _access.CanAsync(User, model.FollowUpOf, TicketOperation.ViewAssigned, cancellationToken);

        // Sending and setting a status changes the ticket, so the status choice needs the edit permission.
        model.CanSetStatus = model.CanEdit;

        if (TempData?.Peek(DraftKey) is string kept && kept.StartsWith(id + "\n", StringComparison.Ordinal))
        {
            var draft = PopDraft(id);
            model.Draft = draft.Body;
            model.DraftIsInternal = draft.IsInternal;
            model.DraftStatusId = model.CanSetStatus ? draft.StatusId : null;
        }

        return View(model);
    }

    /// <summary>
    /// Checks a viewer in on a ticket and answers who else has it open (and is writing on it) and the ticket's
    /// version, so the page warns of a colleague's work and of changes made since it was loaded.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="writing">Whether the viewer is writing a reply or a note.</param>
    /// <param name="leaving">Whether the viewer is closing the page.</param>
    /// <returns>The other viewers and the version.</returns>
    [HttpPost]
    [Admin("ticket/{id}/presence", "TicketsPresence")]
    public async Task<IActionResult> Presence(string id, bool writing = false, bool leaving = false)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket || !await _access.CanAsync(User, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return NotFound();
        }

        var presence = _serviceProvider.GetRequiredService<TicketPresence>();
        var userId = UserId();

        if (leaving)
        {
            await presence.LeaveAsync(id, userId, cancellationToken);

            return Ok();
        }

        var others = await presence.CheckInAsync(id, userId, User.Identity?.Name ?? userId, writing, cancellationToken);

        // The page compares the version with the one it loaded with, to say the ticket changed.
        return Json(new
        {
            viewers = others.Select(viewer => new { name = viewer.Name, writing = viewer.Writing }),
            version = TicketPresenceVersion.Of(ticket),
        });
    }

    /// <summary>
    /// Has the desk's AI assistant write a reply for the agent to edit and send.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The reply as <c>{ text }</c>, or <c>{ error }</c>.</returns>
    [HttpPost]
    [Admin("ticket/{id}/assistant/reply", "TicketsSuggestReply")]
    public async Task<IActionResult> SuggestReply(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var assistant = _serviceProvider.GetService<global::CrestApps.OrchardCore.Tickets.AI.TicketAssistant>();

        if (assistant is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Reply, cancellationToken))
        {
            return Forbid();
        }

        var result = await assistant.SuggestReplyAsync(ticket, cancellationToken);

        return Json(result.Succeeded ? new { text = result.Text, error = (string)null } : new { text = (string)null, error = result.Error });
    }

    /// <summary>
    /// Has the desk's AI assistant triage the ticket into an internal note.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>Back to the ticket.</returns>
    [HttpPost]
    [Admin("ticket/{id}/assistant/triage", "TicketsTriage")]
    public async Task<IActionResult> Triage(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var assistant = _serviceProvider.GetService<global::CrestApps.OrchardCore.Tickets.AI.TicketAssistant>();

        if (assistant is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken))
        {
            return Forbid();
        }

        var result = await assistant.TriageAsync(ticket, cancellationToken);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Starts or stops the user's work on the ticket, with Time Tracker on.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="stop">Whether to stop.</param>
    /// <returns>Back to the ticket.</returns>
    [HttpPost]
    [Admin("ticket/{id}/time", "TicketsTime")]
    public async Task<IActionResult> Time(string id, bool stop = false)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var time = _serviceProvider.GetService<global::CrestApps.OrchardCore.Tickets.TimeTracker.TicketTime>();

        if (time is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Reply, cancellationToken))
        {
            return Forbid();
        }

        // The name the user's time shows, as their own clocking in gives it.
        var name = User.Identity?.Name;

        if (HttpContext.RequestServices.GetService<global::CrestApps.OrchardCore.Users.IDisplayNameProvider>() is { } names
            && await HttpContext.RequestServices.GetRequiredService<global::Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>().GetUserAsync(User) is { } user)
        {
            name = await names.GetAsync(user, cancellationToken) ?? name;
        }

        var result = stop
            ? await time.StopAsync(id, UserId(), name, User.Identity?.Name, cancellationToken)
            : await time.StartAsync(ticket, UserId(), name, User.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Creates a work order from the ticket, with Work Orders on.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="typeId">The work order type.</param>
    /// <param name="priority">The priority, or <see langword="null"/> for the type's.</param>
    /// <returns>Back to the ticket.</returns>
    [HttpPost]
    [Admin("ticket/{id}/work-order", "TicketsCreateWorkOrder")]
    public async Task<IActionResult> CreateWorkOrder(string id, string typeId, global::CrestApps.OrchardCore.WorkOrders.Models.WorkOrderPriority? priority)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var workOrders = _serviceProvider.GetService<global::CrestApps.OrchardCore.Tickets.WorkOrders.TicketWorkOrders>();

        if (workOrders is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken)
            || !await _authorizationService.AuthorizeAsync(User, global::CrestApps.OrchardCore.WorkOrders.WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var result = await workOrders.CreateAsync(id, typeId, priority, Actor(), cancellationToken);

        if (result.WorkOrder is null)
        {
            await _notifier.ErrorAsync(H["The work order wasn't created: {0}", string.Join(" ", result.Errors)]);
        }
        else if (result.Errors.Count > 0)
        {
            await _notifier.WarningAsync(H["Work order {0} was created, but the ticket couldn't link to it: {1}", result.WorkOrder.Number, string.Join(" ", result.Errors)]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Work order {0} was created.", result.WorkOrder.Number]);
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Adds a reply to the requester or an internal note, and optionally sets the status in the same step.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="body">The text.</param>
    /// <param name="isInternal">Whether it's an internal note.</param>
    /// <param name="setStatusId">The status to set with it, if any.</param>
    /// <param name="files">The files to attach.</param>
    /// <param name="replyTemplateId">The reply template used, whose changes to the ticket are applied with the reply.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("ticket/{id}/reply", "TicketsReply")]
    public async Task<IActionResult> Reply(string id, string body, bool isInternal, string setStatusId, List<IFormFile> files, string replyTemplateId = null)
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

        if (!string.IsNullOrEmpty(setStatusId) && !await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken))
        {
            await _notifier.ErrorAsync(H["You may not change this ticket's status."]);

            return await KeepDraftAsync(id, body, isInternal, null);
        }

        var entryId = UniqueId.GenerateId();
        var (attached, uploadError) = await StoreFilesAsync(files, entryId, cancellationToken);

        if (uploadError)
        {
            return await KeepDraftAsync(id, body, isInternal, setStatusId);
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

            return await KeepDraftAsync(id, body, isInternal, setStatusId);
        }

        await _notifier.SuccessAsync(isInternal ? H["The note was added."] : H["The reply was sent."]);
        await ApplyReplyTemplateAsync(result.Ticket ?? ticket, replyTemplateId, statusAlreadySet: !string.IsNullOrEmpty(setStatusId), cancellationToken);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Gives the ticket what its category gives a new ticket: its desk and priority (edit permission, and the desk only one
    /// the user may move tickets to) and its team (assign permission). Changing a category alone never moves a ticket.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="categoryId">The category the page showed, so a category changed meanwhile isn't applied.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("ticket/{id}/category-routing", "TicketsApplyCategoryRouting")]
    public async Task<IActionResult> ApplyCategoryRouting(string id, string categoryId)
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

        var part = ticket.GetOrCreate<TicketPart>();

        if (string.IsNullOrEmpty(part.CategoryId)
            || part.CategoryId != categoryId
            || _serviceProvider.GetService<ITicketCategoryService>() is not { } categories
            || await categories.FindAsync(ticket.ContentType, part.CategoryId, cancellationToken) is not { } category)
        {
            await _notifier.WarningAsync(H["The ticket's category changed while you had it open. Check it and try again."]);

            return RedirectToAction(nameof(Display), new { id });
        }

        var canAssign = await _access.CanAsync(User, ticket, TicketOperation.Assign, cancellationToken);
        var change = new TicketUpdate
        {
            PriorityId = !string.IsNullOrEmpty(category.PriorityId) && category.PriorityId != part.PriorityId ? category.PriorityId : null,
            TeamId = canAssign && !string.IsNullOrEmpty(category.TeamId) && category.TeamId != part.TeamId ? category.TeamId : null,
        };

        if (!string.IsNullOrEmpty(category.DeskId) && category.DeskId != part.DeskId)
        {
            if (await _desks.FindByIdAsync(category.DeskId, cancellationToken) is { } desk
                && (await _access.GetViewScopesAsync(User, cancellationToken)).ContainsKey(desk.ItemId)
                && await _authorizationService.AuthorizeAsync(User, TicketsPermissions.ForDesk(TicketOperation.Edit, desk)))
            {
                change.DeskId = desk.ItemId;
            }
            else
            {
                await _notifier.WarningAsync(H["You may not move tickets to the category's desk, so the ticket stays on its desk."]);
            }
        }

        if (change.DeskId is null && change.PriorityId is null && change.TeamId is null)
        {
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
            await _notifier.SuccessAsync(H["The category's routing was applied."]);
        }

        return result.Succeeded && !await _access.CanAsync(User, result.Ticket, TicketOperation.ViewAssigned, cancellationToken)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Changes the ticket's values from the properties panel: status, priority and desk need the edit permission, team
    /// and assignee the assign permission.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="update">The values.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("ticket/{id}/update", "TicketsUpdate")]
    public async Task<IActionResult> Update(string id, TicketUpdate update, [Bind(Prefix = "loaded")] TicketUpdate loaded)
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

        // Only what the user changed on the form is applied: a value they left as the page showed it stays as it is now,
        // so two agents changing different values don't undo each other. A value they changed that someone else changed
        // since the page was loaded is refused, so nobody overwrites a change they never saw.
        loaded ??= new TicketUpdate();
        var userChanged = (Edited(update.StatusId, loaded.StatusId), Edited(update.PriorityId, loaded.PriorityId), Edited(update.DeskId, loaded.DeskId), Edited(update.TeamId, loaded.TeamId), Edited(update.AssigneeId, loaded.AssigneeId));

        // The category is posted only when the type has categories and the user may edit (the panel shows the picker);
        // an empty choice (posted as nothing) clears it.
        var categoryPosted = Request.HasFormContentType && Request.Form.ContainsKey(nameof(TicketUpdate.CategoryId));
        var categoryChanged = categoryPosted && Edited(update.CategoryId, loaded.CategoryId);

        update.CategoryId = categoryPosted ? update.CategoryId ?? string.Empty : null;

        if ((userChanged.Item1 && Edited(part.StatusId, loaded.StatusId))
            || (userChanged.Item2 && Edited(part.PriorityId, loaded.PriorityId))
            || (userChanged.Item3 && Edited(part.DeskId, loaded.DeskId))
            || (userChanged.Item4 && Edited(part.TeamId, loaded.TeamId))
            || (userChanged.Item5 && Edited(part.AssigneeId, loaded.AssigneeId))
            || (categoryChanged && Edited(part.CategoryId, loaded.CategoryId)))
        {
            await _notifier.WarningAsync(H["Someone else changed this ticket while you had it open. Check its values and try again."]);

            return RedirectToAction(nameof(Display), new { id });
        }

        var change = new TicketUpdate
        {
            StatusId = canEdit && userChanged.Item1 && !string.IsNullOrEmpty(update.StatusId) ? update.StatusId : null,
            PriorityId = canEdit && userChanged.Item2 && !string.IsNullOrEmpty(update.PriorityId) ? update.PriorityId : null,
            DeskId = canEdit && userChanged.Item3 && !string.IsNullOrEmpty(update.DeskId) ? update.DeskId : null,
            TeamId = canAssign && userChanged.Item4 ? update.TeamId ?? string.Empty : null,
            AssigneeId = canAssign && userChanged.Item5 ? update.AssigneeId ?? string.Empty : null,
            CategoryId = canEdit && categoryChanged ? update.CategoryId : null,
        };

        if (change.DeskId is not null && await _desks.FindByIdAsync(change.DeskId, cancellationToken) is { } newDesk
            && (!(await _access.GetViewScopesAsync(User, cancellationToken)).ContainsKey(newDesk.ItemId)
                || !await _authorizationService.AuthorizeAsync(User, TicketsPermissions.ForDesk(TicketOperation.Edit, newDesk))))
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
    [Admin("ticket/{id}/take", "TicketsTake")]
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
    [Admin("ticket/{id}/edit", "TicketsEdit")]
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
    [Admin("ticket/{id}/edit", "TicketsEdit")]
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
    [Admin("ticket/{id}/link", "TicketsLink")]
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

        // A link is only made to a record the user may see: a ticket in their scope, or a record of a kind they may search.
        // Removing a link needs no such check.
        if (!string.IsNullOrEmpty(kind) && !await CanSeeRecordAsync(target, cancellationToken))
        {
            await _notifier.ErrorAsync(H["You can't link to that record."]);

            return RedirectToAction(nameof(Display), new { id });
        }

        await NotifyAsync(await _tickets.LinkAsync(id, target, kind, Actor(), cancellationToken), string.IsNullOrEmpty(kind) ? H["The link was removed."] : H["The link was saved."]);

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Runs a named action on the ticket: the workflows that start with Ticket Action Requested and that name.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="actionName">The action's name.</param>
    /// <returns>The ticket page.</returns>
    [HttpPost]
    [Admin("ticket/{id}/action", "TicketsAction")]
    public async Task<IActionResult> RunAction(string id, string actionName)
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

        if (_serviceProvider.GetService<ITicketActionSource>() is { } actions && await actions.RequestAsync(ticket, actionName, cancellationToken))
        {
            await _notifier.SuccessAsync(H["{0} was started.", actionName]);
        }
        else
        {
            await _notifier.ErrorAsync(H["That action isn't offered on this ticket."]);
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    /// <summary>
    /// Merges the ticket into another one, chosen by its number.
    /// </summary>
    /// <param name="id">The ticket that goes away.</param>
    /// <param name="intoNumber">The number of the ticket it goes into.</param>
    /// <returns>The page of the ticket it went into.</returns>
    [HttpPost]
    [Admin("ticket/{id}/merge", "TicketsMerge")]
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
    [Admin("ticket/{id}/delete", "TicketsDelete")]
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

    // Applies what the reply template used changes on the ticket (a macro), as far as the user may change it.
    private async Task ApplyReplyTemplateAsync(ContentItem ticket, string replyTemplateId, bool statusAlreadySet, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replyTemplateId) || _serviceProvider.GetService<ITicketReplyTemplateSource>() is not { } source
            || await source.FindAsync(replyTemplateId, ticket, cancellationToken) is not { } template)
        {
            return;
        }

        var canEdit = await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken);
        var canAssign = await _access.CanAsync(User, ticket, TicketOperation.Assign, cancellationToken);
        var update = new TicketUpdate
        {
            StatusId = canEdit && !statusAlreadySet ? template.StatusId : null,
            PriorityId = canEdit ? template.PriorityId : null,
            TeamId = canAssign ? template.TeamId : null,
        };

        if (update.StatusId is null && update.PriorityId is null && update.TeamId is null)
        {
            return;
        }

        var result = await _tickets.UpdateAsync(ticket.ContentItemId, update, Actor(), cancellationToken);

        foreach (var error in result.Errors)
        {
            await _notifier.WarningAsync(H["{0}", error]);
        }
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
        var settings = await TypeSettingsAsync(model.ContentType);
        var categories = _serviceProvider.GetService<ITicketCategoryService>() is { } categoryService && !string.IsNullOrEmpty(model.ContentType)
            ? await categoryService.ListAsync(model.ContentType, cancellationToken: cancellationToken)
            : [];

        model.Categories = TicketCategoryForm.Items(categories, settings.CategoryLeavesOnly);
        model.CategoryRequired = settings.CategoryRequired && model.Categories.Count > 0;

        // With categories, the desk can be left to the category (then the ticket type); otherwise it is chosen.
        if (model.Categories.Count == 0)
        {
            model.DeskId ??= offered.FirstOrDefault()?.ItemId;
        }

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

    // The categories of every ticket type (types sharing a taxonomy list it once), indented under the category above.
    private async Task<List<SelectListItem>> CategoryFilterOptionsAsync(string selectedId, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<ITicketCategoryService>() is not { } categories)
        {
            return [];
        }

        var options = new List<SelectListItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in await TicketTypesAsync())
        {
            foreach (var category in await categories.ListAsync(type.Value, cancellationToken: cancellationToken))
            {
                if (seen.Add(category.Id))
                {
                    options.Add(new SelectListItem(new string(' ', category.Level * 3) + category.Name, category.Id, category.Id == selectedId));
                }
            }
        }

        return options;
    }

    private async Task<TicketPartSettings> TypeSettingsAsync(string contentType)
        => string.IsNullOrEmpty(contentType)
            ? new TicketPartSettings()
            : (await _contentDefinitionManager.GetTypeDefinitionAsync(contentType))?.Parts
                .FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>() ?? new TicketPartSettings();

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

        var editableDesks = new HashSet<string>(StringComparer.Ordinal);

        foreach (var desk in lookups.Desks.Values)
        {
            if (await _authorizationService.AuthorizeAsync(User, TicketsPermissions.ForDesk(TicketOperation.Edit, desk)))
            {
                editableDesks.Add(desk.ItemId);
            }
        }

        return [.. parts.Select(item => new TicketRow
        {
            Ticket = item.Ticket,
            Part = item.Part,
            CanEdit = editableDesks.Contains(item.Part.DeskId ?? string.Empty) && !TicketLifecycle.IsReadOnly(item.Part),
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
        var (attached, errors) = await _serviceProvider.GetRequiredService<TicketUploads>().StoreAsync(files, entryId, CurrentParty(), cancellationToken);

        foreach (var error in errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        return (attached, errors.Count > 0);
    }

    private Task DeleteFilesAsync(List<string> attachmentIds)
        => _serviceProvider.GetRequiredService<TicketUploads>().DeleteAsync(attachmentIds);

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

    // The draft travels in the TempData cookie, so a long one isn't kept: it would push the request headers past what
    // servers accept.
    // The draft keeps who it was for and the status chosen with it, so a refused internal note never comes back as a reply
    // to the requester.
    private async Task<RedirectToActionResult> KeepDraftAsync(string id, string body, bool isInternal, string setStatusId)
    {
        if (body?.Length > TimelineConstants.MaxKeptDraftLength)
        {
            await _notifier.WarningAsync(H["Your text was too long to keep for another try. Copy a long text before you send it."]);
        }
        else if (!string.IsNullOrEmpty(body))
        {
            TempData[DraftKey] = string.Join("\n", id, isInternal ? "note" : "reply", setStatusId ?? string.Empty, body);
        }

        return RedirectToAction(nameof(Display), new { id });
    }

    private (string Body, bool IsInternal, string StatusId) PopDraft(string id)
    {
        var parts = ((string)TempData[DraftKey]).Split('\n', 4);

        // A draft kept by an older version holds only the text: it comes back as a note, never as a public reply.
        return parts.Length == 4 && parts[1] is "note" or "reply"
            ? (parts[3], parts[1] == "note", string.IsNullOrEmpty(parts[2]) ? null : parts[2])
            : (string.Join("\n", parts.Skip(1)), true, null);
    }

    private async Task<bool> CanSeeRecordAsync(RecordRef record, CancellationToken cancellationToken)
    {
        if (record.Kind == TicketsConstants.RecordKind)
        {
            return await _tickets.GetAsync(record.Id) is { } linked && await _access.CanAsync(User, linked, TicketOperation.ViewAssigned, cancellationToken);
        }

        return _serviceProvider.GetService<IRecordDirectory>()?.GetSource(record.Kind) is { } source && await source.CanSearchAsync(User, cancellationToken);
    }

    private static bool Edited(string value, string loaded)
        => !string.Equals(string.IsNullOrEmpty(value) ? null : value, string.IsNullOrEmpty(loaded) ? null : loaded, StringComparison.Ordinal);

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

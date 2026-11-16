using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// The ticket boards: the list of boards the user may open, the editor of a board's columns and filters, and the board
/// itself, where agents drag cards between columns to change a ticket's status. A board only ever shows the tickets the
/// user may see, and every move goes through <see cref="ITicketBoardService.MoveAsync"/>.
/// </summary>
[Admin]
public sealed class TicketBoardsController : Controller
{
    private const string ValidationPrefix = nameof(TicketBoard);

    private readonly ITicketBoardService _boards;
    private readonly ICatalogManager<TicketBoard> _manager;
    private readonly ITicketAccessService _access;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IPartyDirectory _parties;
    private readonly IDisplayManager<TicketBoard> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBoardsController"/> class.
    /// </summary>
    public TicketBoardsController(
        ITicketBoardService boards,
        ICatalogManager<TicketBoard> manager,
        ITicketAccessService access,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        ICatalog<TicketDesk> desks,
        IPartyDirectory parties,
        IDisplayManager<TicketBoard> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        ILocalClock localClock,
        IClock clock,
        IServiceProvider serviceProvider,
        IHtmlLocalizer<TicketBoardsController> htmlLocalizer,
        IStringLocalizer<TicketBoardsController> stringLocalizer)
    {
        _boards = boards;
        _manager = manager;
        _access = access;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _parties = parties;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _localClock = localClock;
        _clock = clock;
        _serviceProvider = serviceProvider;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the boards the user may open.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("tickets/boards", "TicketBoardsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await CanViewTicketsAsync())
        {
            return Forbid();
        }

        return View(new TicketBoardsIndexViewModel
        {
            List = new AdminListContext<TicketBoard> { Source = await _boards.ListVisibleAsync(User, HttpContext.RequestAborted) },
            CanCreate = await _boards.CanCreateAsync(User),
        });
    }

    /// <summary>
    /// Shows the editor of a new board.
    /// </summary>
    /// <returns>The editor.</returns>
    [Admin("tickets/boards/create", "TicketBoardsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _boards.CanCreateAsync(User))
        {
            return Forbid();
        }

        var board = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New board"],
            Editor = await _displayManager.BuildEditorAsync(board, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates a board.
    /// </summary>
    /// <returns>The new board, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("tickets/boards/create", "TicketBoardsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _boards.CanCreateAsync(User))
        {
            return Forbid();
        }

        var board = await _manager.NewAsync();
        var editor = await _displayManager.UpdateEditorAsync(board, _updateModelAccessor.ModelUpdater, isNew: true);
        var rulesPassed = await CatalogEntryValidation.ValidateAsync(_manager, board, _updateModelAccessor.ModelUpdater, ValidationPrefix, HttpContext.RequestAborted);

        if (rulesPassed && await MayKeepAsync(board) && ModelState.IsValid)
        {
            await _manager.CreateAsync(board);
            await _notifier.SuccessAsync(H["The board {0} has been created.", board.Name]);

            return RedirectToAction(nameof(Display), new { id = board.ItemId });
        }

        return View(new EditCatalogEntryViewModel { DisplayName = S["New board"], Editor = editor });
    }

    /// <summary>
    /// Shows the editor of a board.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <returns>The editor.</returns>
    [Admin("tickets/boards/edit/{id}", "TicketBoardsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        var (board, result) = await FindManageableAsync(id);

        if (result is not null)
        {
            return result;
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = board.Name,
            Editor = await _displayManager.BuildEditorAsync(board, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a board.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <returns>The board, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("tickets/boards/edit/{id}", "TicketBoardsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        var (board, result) = await FindManageableAsync(id);

        if (result is not null)
        {
            return result;
        }

        var name = board.Name;
        var editor = await _displayManager.UpdateEditorAsync(board, _updateModelAccessor.ModelUpdater, isNew: false);
        var rulesPassed = await CatalogEntryValidation.ValidateAsync(_manager, board, _updateModelAccessor.ModelUpdater, ValidationPrefix, HttpContext.RequestAborted);

        if (rulesPassed && await MayKeepAsync(board) && ModelState.IsValid)
        {
            await _manager.UpdateAsync(board);
            await _notifier.SuccessAsync(H["The board {0} has been saved.", board.Name]);

            // A board made personal or shared with a team the user isn't in may no longer be theirs to open.
            return await _boards.FindVisibleAsync(User, board.ItemId, HttpContext.RequestAborted) is null
                ? RedirectToAction(nameof(Index))
                : RedirectToAction(nameof(Display), new { id = board.ItemId });
        }

        return View(new EditCatalogEntryViewModel { DisplayName = name, Editor = editor });
    }

    /// <summary>
    /// Deletes a board. The tickets on it stay as they are.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("tickets/boards/delete/{id}", "TicketBoardsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var (board, result) = await FindManageableAsync(id);

        if (result is not null)
        {
            return result;
        }

        if (await _manager.DeleteAsync(board))
        {
            await _notifier.SuccessAsync(H["The board {0} has been deleted.", board.Name]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a board.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <returns>The page.</returns>
    [Admin("tickets/board/{id}", "TicketBoardsDisplay")]
    public async Task<IActionResult> Display(string id)
    {
        var boards = await _boards.ListVisibleAsync(User, HttpContext.RequestAborted);

        if (boards.FirstOrDefault(board => board.ItemId == id) is not { } board)
        {
            return NotFound();
        }

        return View(new TicketBoardPageViewModel
        {
            Board = board,
            Boards = [.. boards],
            CanManage = await _boards.CanManageAsync(User, board),
            HubUrl = HttpContext.RequestServices.GetService(typeof(RealTime.TicketBoardNotifier)) is null ? null : CrestApps.OrchardCore.SignalRHubRoutes.GetTenantAwareHubUrl<RealTime.TicketBoardHub>(HttpContext),
        });
    }

    /// <summary>
    /// Returns a board's columns and cards, as the user may see them.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <param name="q">The text to find in the number or subject.</param>
    /// <param name="assignee">Whose tickets to show, if not the board's own.</param>
    /// <returns>The columns and cards, as JSON.</returns>
    [Admin("tickets/board/{id}/data", "TicketBoardsData")]
    public async Task<IActionResult> Data(string id, string q = null, string assignee = null)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _boards.FindVisibleAsync(User, id, cancellationToken) is not { } board)
        {
            return NotFound();
        }

        var filter = new TicketBoardFilter
        {
            Search = q,
            Assignee = assignee is TicketAssignees.Me or TicketAssignees.Unassigned ? assignee : null,
        };
        var columns = await _boards.LoadAsync(User, board, filter, cancellationToken);
        var cards = await CardsAsync(columns.SelectMany(column => column.Tickets).ToList(), board, cancellationToken);
        var shown = columns.SelectMany(column => column.Statuses).Select(status => status.ItemId).ToHashSet(StringComparer.Ordinal);

        return Json(new
        {
            board = new { id = board.ItemId, name = board.Name },
            columns = columns.Select(column => new
            {
                id = column.Column.Id,
                name = column.Column.Name,
                limit = column.Column.Limit,
                defaultStatusId = column.Column.DefaultStatusId,
                total = column.Total,
                statuses = column.Statuses.Select(status => new { id = status.ItemId, name = status.Name, color = status.Color, category = status.Category.ToString() }),
                cards = column.Tickets.Select(ticket => cards[ticket.ContentItemId]),
            }),
            transitions = Enum.GetValues<TicketStatusCategory>().ToDictionary(category => category.ToString(), category => TicketLifecycle.Transitions.Next(category).Select(next => next.ToString()).Append(category.ToString())),
            hasHiddenStatuses = (await _statuses.GetAllAsync(cancellationToken)).Any(status => status.Category != TicketStatusCategory.Closed && !shown.Contains(status.ItemId)),
        });
    }

    /// <summary>
    /// Moves a card to a column: sets the ticket's status to the column's status, the one chosen, or asks which one.
    /// </summary>
    /// <param name="id">The board.</param>
    /// <param name="move">The move.</param>
    /// <returns>What the move did, as JSON.</returns>
    [HttpPost]
    [Admin("tickets/board/{id}/move", "TicketBoardsMove")]
    public async Task<IActionResult> Move(string id, TicketBoardMove move)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (move is null || await _boards.FindVisibleAsync(User, id, cancellationToken) is not { } board)
        {
            return NotFound();
        }

        var result = await _boards.MoveAsync(User, board, move, cancellationToken);
        var card = result.Ticket is { } ticket && await _access.CanAsync(User, ticket, TicketOperation.ViewAssigned, cancellationToken)
            ? (await CardsAsync([ticket], board, cancellationToken)).GetValueOrDefault(ticket.ContentItemId)
            : null;

        return Json(new
        {
            succeeded = result.Succeeded,
            changed = result.Changed,
            stale = result.Stale,
            errors = result.Errors,
            choices = result.Choices.Select(status => new { id = status.ItemId, name = status.Name, color = status.Color }),
            card,
        });
    }

    private async Task<Dictionary<string, object>> CardsAsync(IReadOnlyList<ContentItem> tickets, TicketBoard board, CancellationToken cancellationToken)
    {
        var parts = tickets.Select(ticket => (Ticket: ticket, Part: ticket.GetOrCreate<TicketPart>() ?? new TicketPart())).ToList();
        var statuses = (await _statuses.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var priorities = (await _priorities.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var desks = (await _desks.GetAllAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var names = await _parties.GetNamesAsync(
            parts.SelectMany(item => new[] { item.Part.Requester, string.IsNullOrEmpty(item.Part.AssigneeId) ? default : new PartyRef(PartyKinds.User, item.Part.AssigneeId) })
                .Where(party => !party.IsEmpty)
                .Distinct()
                .ToList(),
            cancellationToken);
        var teamNames = _serviceProvider.GetService<ITeamManager>() is { } teams
            ? (await teams.ListTreeAsync(cancellationToken)).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal)
            : [];

        // The desk is shown when the board spans desks; a board of one desk needs no reminder of it.
        var showDesk = board.Query?.DeskIds is not { Count: 1 };
        var now = _clock.UtcNow;
        var cards = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (ticket, part) in parts)
        {
            var status = statuses.GetValueOrDefault(part.StatusId ?? string.Empty);
            var priority = priorities.GetValueOrDefault(part.PriorityId ?? string.Empty);
            var updatedUtc = ticket.ModifiedUtc ?? ticket.CreatedUtc ?? now;

            cards[ticket.ContentItemId] = new
            {
                id = ticket.ContentItemId,
                number = part.Number,
                subject = ticket.DisplayText,
                url = Url.Action("Display", "Tickets", new { area = TicketsConstants.Features.Tickets, id = ticket.ContentItemId }),
                statusId = part.StatusId,
                statusName = status?.Name,
                statusColor = status?.Color,
                category = part.StatusCategory.ToString(),
                priorityName = priority?.Name,
                priorityColor = priority?.Color,
                priorityRank = priority?.Rank ?? 0,
                assigneeName = string.IsNullOrEmpty(part.AssigneeId) ? null : names.GetValueOrDefault(new PartyRef(PartyKinds.User, part.AssigneeId), part.AssigneeId),
                requesterName = !part.Requester.IsEmpty && names.TryGetValue(part.Requester, out var requesterName)
                    ? requesterName
                    : part.RequesterContact?.Name ?? part.RequesterContact?.Email,
                teamName = teamNames.GetValueOrDefault(part.TeamId ?? string.Empty),
                deskName = showDesk ? desks.GetValueOrDefault(part.DeskId ?? string.Empty)?.Name : null,
                overdue = part.NextDueUtc is { } due && due <= now,
                dueText = part.NextDueUtc is { } next && next > now ? await LocalAsync(next) : null,
                updatedUtc = DateTime.SpecifyKind(updatedUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
                updatedText = await LocalAsync(updatedUtc),
                canMove = !TicketLifecycle.IsReadOnly(part) && await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken),
            };
        }

        return cards;
    }

    private async Task<string> LocalAsync(DateTime utc)
        => (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture);

    // The board as saved must be one the user may manage: sharing it with everybody or a team takes the Manage shared
    // ticket boards permission.
    private async Task<bool> MayKeepAsync(TicketBoard board)
    {
        if (await _boards.CanManageAsync(User, board))
        {
            return true;
        }

        ModelState.AddModelError($"{ValidationPrefix}.{nameof(TicketBoardViewModel.Sharing)}", S["You may keep boards for yourself only; sharing a board takes the Manage shared ticket boards permission."]);

        return false;
    }

    // A board the user may not open is answered as missing; one they may open but not arrange is forbidden.
    private async Task<(TicketBoard Board, IActionResult Result)> FindManageableAsync(string id)
    {
        if (await _boards.FindVisibleAsync(User, id, HttpContext.RequestAborted) is not { } board)
        {
            return (null, NotFound());
        }

        return await _boards.CanManageAsync(User, board) ? (board, null) : (null, Forbid());
    }

    private async Task<bool> CanViewTicketsAsync()
        => (await _access.GetViewScopesAsync(User, HttpContext.RequestAborted)).Count > 0;
}

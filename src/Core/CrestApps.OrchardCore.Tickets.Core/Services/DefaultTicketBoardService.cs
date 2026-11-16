using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The default <see cref="ITicketBoardService"/>. A board's tickets are listed through the viewer's desk scope, one
/// query per column; a move sets the status through <see cref="ITicketService.UpdateAsync"/>, so the life cycle,
/// approval holds and every rule of a status change apply as they do on the ticket page.
/// </summary>
public sealed class DefaultTicketBoardService : ITicketBoardService
{
    private static readonly IReadOnlySet<string> _noTeams = new HashSet<string>(StringComparer.Ordinal);

    private readonly ICatalog<TicketBoard> _boards;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ITeamMembershipService _memberships;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTicketBoardService"/> class.
    /// </summary>
    public DefaultTicketBoardService(
        ICatalog<TicketBoard> boards,
        ICatalog<TicketStatus> statuses,
        ITicketService tickets,
        ITicketAccessService access,
        ITeamMembershipService memberships,
        IAuthorizationService authorizationService,
        IStringLocalizer<DefaultTicketBoardService> stringLocalizer)
    {
        _boards = boards;
        _statuses = statuses;
        _tickets = tickets;
        _access = access;
        _memberships = memberships;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketBoard>> ListVisibleAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if ((await _access.GetViewScopesAsync(user, cancellationToken)).Count == 0)
        {
            return [];
        }

        var userId = UserIdOf(user);
        var boards = await _boards.GetAllAsync(cancellationToken);
        var teams = boards.Any(board => !string.IsNullOrEmpty(board.SharedWithTeamId))
            ? await TeamsOfAsync(userId, cancellationToken)
            : _noTeams;

        return [.. boards
            .Where(board => IsVisible(board, userId, teams))
            .OrderBy(board => board.Order)
            .ThenBy(board => board.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <inheritdoc/>
    public async Task<TicketBoard> FindVisibleAsync(ClaimsPrincipal user, string boardId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(boardId)
            || await _boards.FindByIdAsync(boardId, cancellationToken) is not { } board
            || (await _access.GetViewScopesAsync(user, cancellationToken)).Count == 0)
        {
            return null;
        }

        var userId = UserIdOf(user);
        var teams = string.IsNullOrEmpty(board.SharedWithTeamId) ? _noTeams : await TeamsOfAsync(userId, cancellationToken);

        return IsVisible(board, userId, teams) ? board : null;
    }

    /// <inheritdoc/>
    public async Task<bool> CanCreateAsync(ClaimsPrincipal user)
        => await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ManageOwnTicketBoards);

    /// <inheritdoc/>
    public async Task<bool> CanShareAsync(ClaimsPrincipal user)
        => await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ManageTicketBoards);

    /// <inheritdoc/>
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, TicketBoard board)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(board);

        if (string.IsNullOrEmpty(board.OwnerId))
        {
            return await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ManageTicketBoards);
        }

        // A personal board is its owner's alone; not even a manager of shared boards rearranges it.
        return string.Equals(board.OwnerId, UserIdOf(user), StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ManageOwnTicketBoards);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketBoardColumnTickets>> LoadAsync(ClaimsPrincipal user, TicketBoard board, TicketBoardFilter filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(board);

        var statuses = (await _statuses.GetAllAsync(cancellationToken)).OrderBy(status => status.Order).ThenBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var columns = new List<TicketBoardColumnTickets>();

        foreach (var column in board.Columns ?? [])
        {
            var columnStatuses = statuses.Where(status => column.StatusIds?.Contains(status.ItemId, StringComparer.Ordinal) == true).ToList();

            // A query with no status lists every status, so a column whose statuses are all gone shows nothing instead.
            if (columnStatuses.Count == 0)
            {
                columns.Add(new TicketBoardColumnTickets { Column = column });

                continue;
            }

            var scoped = await _access.ApplyScopeAsync(user, QueryOf(board, filter, [.. columnStatuses.Select(status => status.ItemId)]), cancellationToken);

            if (scoped is null)
            {
                columns.Add(new TicketBoardColumnTickets { Column = column, Statuses = columnStatuses });

                continue;
            }

            var (tickets, total) = await _tickets.ListAsync(scoped, cancellationToken);

            columns.Add(new TicketBoardColumnTickets
            {
                Column = column,
                Statuses = columnStatuses,
                Tickets = tickets,
                Total = total,
            });
        }

        return columns;
    }

    /// <inheritdoc/>
    public async Task<TicketBoardMoveResult> MoveAsync(ClaimsPrincipal user, TicketBoard board, TicketBoardMove move, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(move);

        var column = board.Columns?.FirstOrDefault(item => string.Equals(item.Id, move.ColumnId, StringComparison.Ordinal));

        if (column is null)
        {
            return Refused(S["That column isn't on the board any more. Load the board again."], stale: true);
        }

        // A ticket the user may not see is answered as one that doesn't exist.
        if (string.IsNullOrEmpty(move.TicketId)
            || await _tickets.GetAsync(move.TicketId) is not { } ticket
            || !await _access.CanAsync(user, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return Refused(S["The ticket wasn't found."], stale: true);
        }

        var part = ticket.GetOrCreate<TicketPart>();

        if (!await _access.CanAsync(user, ticket, TicketOperation.Edit, cancellationToken))
        {
            return Refused(S["You may not change ticket {0}.", part.Number], ticket);
        }

        if (!string.IsNullOrEmpty(move.ShownStatusId) && !string.Equals(move.ShownStatusId, part.StatusId, StringComparison.Ordinal))
        {
            return Refused(S["Someone else changed ticket {0} while you had the board open. The board shows it as it is now.", part.Number], ticket, stale: true);
        }

        var statuses = (await _statuses.GetAllAsync(cancellationToken)).OrderBy(status => status.Order).ThenBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var target = TicketBoardColumns.Target(column, [.. statuses.Select(status => status.ItemId)], part.StatusId, move.StatusId);

        switch (target.Kind)
        {
            case TicketBoardTargetKind.Unchanged:
                return new TicketBoardMoveResult { Ticket = ticket };

            case TicketBoardTargetKind.Choose:
                return new TicketBoardMoveResult
                {
                    Ticket = ticket,
                    Choices = [.. statuses.Where(status => target.Choices.Contains(status.ItemId, StringComparer.Ordinal))],
                };

            case TicketBoardTargetKind.NotInColumn:
                return Refused(S["That status isn't in the column {0}.", column.Name], ticket);

            case TicketBoardTargetKind.NoStatus:
                return Refused(S["The column {0} has no status, so no ticket can move to it.", column.Name], ticket);
        }

        var result = await _tickets.UpdateAsync(
            ticket.ContentItemId,
            new TicketUpdate
            {
                StatusId = target.StatusId,
                Reason = S["Moved on the board {0}.", board.Name],
            },
            TicketActor.User(UserIdOf(user), user.Identity?.Name),
            cancellationToken);

        return result.Succeeded
            ? new TicketBoardMoveResult { Ticket = result.Ticket, Changed = true }
            : new TicketBoardMoveResult { Ticket = result.Ticket ?? ticket, Errors = result.Errors };
    }

    /// <summary>
    /// Returns whether a user may open a board: it is shared with everybody, it is theirs, or it is shared with one of
    /// their teams.
    /// </summary>
    /// <param name="board">The board.</param>
    /// <param name="userId">The user.</param>
    /// <param name="teamIds">The user's teams.</param>
    /// <returns>Whether they may.</returns>
    public static bool IsVisible(TicketBoard board, string userId, IReadOnlySet<string> teamIds)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!string.IsNullOrEmpty(board.SharedWithTeamId) && teamIds?.Contains(board.SharedWithTeamId) == true)
        {
            return true;
        }

        return string.IsNullOrEmpty(board.OwnerId)
            ? string.IsNullOrEmpty(board.SharedWithTeamId)
            : !string.IsNullOrEmpty(userId) && string.Equals(board.OwnerId, userId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the query of one column: the board's desks, teams, ticket types, priorities and assignee (narrowed by
    /// what the viewer searches for), the column's statuses, and at most <see cref="ITicketBoardService.CardsPerColumn"/>
    /// tickets.
    /// </summary>
    /// <param name="board">The board.</param>
    /// <param name="filter">What the viewer narrows the board to, if anything.</param>
    /// <param name="statusIds">The column's statuses.</param>
    /// <returns>The query, before the viewer's scope is applied.</returns>
    public static TicketQuery QueryOf(TicketBoard board, TicketBoardFilter filter, IReadOnlyList<string> statusIds)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(statusIds);

        var start = board.Query ?? new TicketQuery();

        return new TicketQuery
        {
            Search = string.IsNullOrWhiteSpace(filter?.Search) ? start.Search : filter.Search.Trim(),
            DeskIds = [.. start.DeskIds ?? []],
            Categories = [],
            StatusIds = [.. statusIds],
            PriorityIds = [.. start.PriorityIds ?? []],
            TeamIds = [.. start.TeamIds ?? []],
            Assignee = string.IsNullOrWhiteSpace(filter?.Assignee) ? start.Assignee : filter.Assignee,
            ContentTypes = [.. start.ContentTypes ?? []],
            Requester = start.Requester,
            AccountId = start.AccountId,
            DueWithin = start.DueWithin,
            Sort = start.Sort,
            Skip = 0,
            Take = ITicketBoardService.CardsPerColumn,
        };
    }

    private async Task<IReadOnlySet<string>> TeamsOfAsync(string userId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(userId)
            ? _noTeams
            : (await _memberships.GetMembershipsAsync(userId, cancellationToken: cancellationToken)).Select(member => member.TeamId).ToHashSet(StringComparer.Ordinal);

    private static string UserIdOf(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static TicketBoardMoveResult Refused(string error, ContentItem ticket = null, bool stale = false)
        => new() { Ticket = ticket, Errors = [error], Stale = stale };
}

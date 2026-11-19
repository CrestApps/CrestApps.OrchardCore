using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// Keeps a board's columns sound, from the editor and from a recipe alike: at least one column and at most
/// <see cref="TicketBoard.MaxColumns"/>; each with a name and a status that exists; a status in one column only; a
/// default that is one of the column's statuses. Each column gets an id, and the board's query keeps no statuses, which
/// the columns decide. The name and the owner are checked by <see cref="TicketSettingsRecordHandler{T}"/>.
/// </summary>
public sealed class TicketBoardHandler : CatalogEntryHandlerBase<TicketBoard>
{
    private readonly ICatalog<TicketStatus> _statuses;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBoardHandler"/> class.
    /// </summary>
    /// <param name="statuses">The statuses.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketBoardHandler(ICatalog<TicketStatus> statuses, IStringLocalizer<TicketBoardHandler> stringLocalizer)
    {
        _statuses = statuses;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<TicketBoard> context, CancellationToken cancellationToken = default)
    {
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<TicketBoard> context, CancellationToken cancellationToken = default)
    {
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TicketBoard> context, CancellationToken cancellationToken = default)
    {
        var board = context.Model;

        Normalize(board);

        if (board.Columns.Count == 0)
        {
            Fail(S["Add at least one column."]);

            return;
        }

        if (board.Columns.Count > TicketBoard.MaxColumns)
        {
            Fail(S["A board has at most {0} columns.", TicketBoard.MaxColumns]);
        }

        var statuses = (await _statuses.GetAllAsync(cancellationToken)).ToDictionary(status => status.ItemId, StringComparer.Ordinal);
        var columnOfStatus = new Dictionary<string, TicketBoardColumn>(StringComparer.Ordinal);

        foreach (var column in board.Columns)
        {
            var name = string.IsNullOrEmpty(column.Name) ? S["(no name)"].Value : column.Name;

            if (string.IsNullOrEmpty(column.Name))
            {
                Fail(S["Each column needs a name."]);
            }

            if (column.StatusIds.Count == 0)
            {
                Fail(S["The column {0} has no status. Choose at least one.", name]);
            }

            foreach (var statusId in column.StatusIds)
            {
                if (!statuses.TryGetValue(statusId, out var status))
                {
                    Fail(S["The column {0} has a status that doesn't exist: {1}.", name, statusId]);

                    continue;
                }

                if (columnOfStatus.TryGetValue(statusId, out var other))
                {
                    Fail(S["The status {0} is in the columns {1} and {2}; a status can be in one column only.", status.Name, other.Name, name]);
                }
                else
                {
                    columnOfStatus[statusId] = column;
                }
            }

            if (!string.IsNullOrEmpty(column.DefaultStatusId) && !column.StatusIds.Contains(column.DefaultStatusId, StringComparer.Ordinal))
            {
                Fail(S["The default status of the column {0} isn't one of its statuses.", name]);
            }
        }

        void Fail(LocalizedString message)
            => context.Result.Fail(new ValidationResult(message, [nameof(TicketBoard.Columns)]));
    }

    /// <summary>
    /// Tidies a board: trims names, drops empty and repeated statuses, gives each column an id, and clears the query's
    /// statuses, which the columns decide.
    /// </summary>
    /// <param name="board">The board.</param>
    public static void Normalize(TicketBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);

        board.Name = board.Name?.Trim();
        board.Description = string.IsNullOrWhiteSpace(board.Description) ? null : board.Description.Trim();
        board.Columns = [.. (board.Columns ?? []).Where(column => column is not null)];
        board.Query ??= new TicketQuery();
        board.Query.StatusIds = [];
        board.Query.Categories = [];
        board.Query.Skip = 0;
        board.OwnerId = string.IsNullOrWhiteSpace(board.OwnerId) ? null : board.OwnerId;
        board.SharedWithTeamId = string.IsNullOrWhiteSpace(board.SharedWithTeamId) ? null : board.SharedWithTeamId;

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in board.Columns)
        {
            column.Name = column.Name?.Trim();
            column.StatusIds = [.. (column.StatusIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal)];
            column.DefaultStatusId = string.IsNullOrWhiteSpace(column.DefaultStatusId) ? null : column.DefaultStatusId.Trim();
            column.Limit = column.Limit is > 0 ? column.Limit : null;

            // A column keeps its id from save to save, so a board open in another tab still moves cards to it.
            if (string.IsNullOrWhiteSpace(column.Id) || !ids.Add(column.Id.Trim()))
            {
                do
                {
                    column.Id = Guid.NewGuid().ToString("n")[..12];
                }
                while (!ids.Add(column.Id));
            }
            else
            {
                column.Id = column.Id.Trim();
            }
        }
    }
}

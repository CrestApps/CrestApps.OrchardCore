using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// How a board's columns map to statuses: the column a ticket shows in, and the status a card dropped on a column gets.
/// </summary>
public static class TicketBoardColumns
{
    /// <summary>
    /// Returns the column that shows tickets in a status.
    /// </summary>
    /// <param name="board">The board.</param>
    /// <param name="statusId">The status.</param>
    /// <returns>The column, or <see langword="null"/> when no column has the status.</returns>
    public static TicketBoardColumn ColumnOf(TicketBoard board, string statusId)
    {
        ArgumentNullException.ThrowIfNull(board);

        return string.IsNullOrEmpty(statusId)
            ? null
            : board.Columns?.FirstOrDefault(column => column.StatusIds?.Contains(statusId, StringComparer.Ordinal) == true);
    }

    /// <summary>
    /// Returns the status a card dropped on a column gets: the one chosen, the column's default, or its only status. A
    /// card dropped on the column it is already in stays as it is; a column with several statuses and no default asks.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <param name="existingStatusIds">The statuses that exist, in the order of the status list.</param>
    /// <param name="currentStatusId">The ticket's status now.</param>
    /// <param name="chosenStatusId">The status chosen, if any.</param>
    /// <returns>What the drop does.</returns>
    public static TicketBoardTarget Target(TicketBoardColumn column, IReadOnlyList<string> existingStatusIds, string currentStatusId, string chosenStatusId)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(existingStatusIds);

        var statuses = existingStatusIds.Where(id => column.StatusIds?.Contains(id, StringComparer.Ordinal) == true).ToList();

        if (statuses.Count == 0)
        {
            return new TicketBoardTarget(TicketBoardTargetKind.NoStatus);
        }

        if (!string.IsNullOrEmpty(chosenStatusId))
        {
            if (!statuses.Contains(chosenStatusId, StringComparer.Ordinal))
            {
                return new TicketBoardTarget(TicketBoardTargetKind.NotInColumn);
            }

            return string.Equals(chosenStatusId, currentStatusId, StringComparison.Ordinal)
                ? new TicketBoardTarget(TicketBoardTargetKind.Unchanged, chosenStatusId)
                : new TicketBoardTarget(TicketBoardTargetKind.Status, chosenStatusId);
        }

        // Dropped back on its own column: nothing to change, and nothing to ask.
        if (!string.IsNullOrEmpty(currentStatusId) && statuses.Contains(currentStatusId, StringComparer.Ordinal))
        {
            return new TicketBoardTarget(TicketBoardTargetKind.Unchanged, currentStatusId);
        }

        if (!string.IsNullOrEmpty(column.DefaultStatusId) && statuses.Contains(column.DefaultStatusId, StringComparer.Ordinal))
        {
            return new TicketBoardTarget(TicketBoardTargetKind.Status, column.DefaultStatusId);
        }

        return statuses.Count == 1
            ? new TicketBoardTarget(TicketBoardTargetKind.Status, statuses[0])
            : new TicketBoardTarget(TicketBoardTargetKind.Choose, Choices: statuses);
    }
}

/// <summary>
/// What a card dropped on a column does.
/// </summary>
public enum TicketBoardTargetKind
{
    /// <summary>
    /// The ticket gets <see cref="TicketBoardTarget.StatusId"/>.
    /// </summary>
    Status,

    /// <summary>
    /// The ticket is already in the column (or the status chosen); nothing changes.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The column has several statuses and no default: one of <see cref="TicketBoardTarget.Choices"/> has to be chosen.
    /// </summary>
    Choose,

    /// <summary>
    /// The status chosen isn't one of the column's.
    /// </summary>
    NotInColumn,

    /// <summary>
    /// The column has no status that still exists.
    /// </summary>
    NoStatus,
}

/// <summary>
/// What a card dropped on a column does, and the status it gets or the statuses to choose from.
/// </summary>
/// <param name="Kind">What the drop does.</param>
/// <param name="StatusId">The status the ticket gets, or is already in.</param>
/// <param name="Choices">The statuses to choose from.</param>
public sealed record TicketBoardTarget(TicketBoardTargetKind Kind, string StatusId = null, IReadOnlyList<string> Choices = null);

using CrestApps.OrchardCore.StateMachines;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The rules of a ticket's life: which status categories may follow which, when a requester's reply reopens a solved
/// ticket, when a solved ticket closes on its own, and which tickets can no longer change.
/// </summary>
public static class TicketLifecycle
{
    /// <summary>
    /// The moves between status categories. A closed ticket is final; a solved one can reopen.
    /// </summary>
    public static readonly TransitionTable<TicketStatusCategory> Transitions = new(
        new Dictionary<TicketStatusCategory, TicketStatusCategory[]>
        {
            [TicketStatusCategory.New] = [TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold, TicketStatusCategory.Solved, TicketStatusCategory.Closed],
            [TicketStatusCategory.Open] = [TicketStatusCategory.New, TicketStatusCategory.Pending, TicketStatusCategory.OnHold, TicketStatusCategory.Solved, TicketStatusCategory.Closed],
            [TicketStatusCategory.Pending] = [TicketStatusCategory.Open, TicketStatusCategory.OnHold, TicketStatusCategory.Solved, TicketStatusCategory.Closed],
            [TicketStatusCategory.OnHold] = [TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.Solved, TicketStatusCategory.Closed],
            [TicketStatusCategory.Solved] = [TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold, TicketStatusCategory.Closed],
            [TicketStatusCategory.Closed] = [],
        },
        allowStaying: true);

    /// <summary>
    /// Returns whether a ticket can no longer change: it is closed or was merged into another.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <returns>Whether it is read-only.</returns>
    public static bool IsReadOnly(TicketPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.StatusCategory == TicketStatusCategory.Closed || !string.IsNullOrEmpty(part.MergedIntoId);
    }

    /// <summary>
    /// Returns whether a requester's reply reopens a ticket: it is solved and still inside the desk's reopen window.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <param name="desk">Its desk, or <see langword="null"/>.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>Whether the reply reopens it.</returns>
    public static bool ReopensOnRequesterReply(TicketPart part, TicketDesk desk, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (part.StatusCategory != TicketStatusCategory.Solved)
        {
            return false;
        }

        var window = Math.Max(desk?.ReopenWindowDays ?? 7, 0);

        return part.SolvedUtc is not { } solved || solved.AddDays(window) >= nowUtc;
    }

    /// <summary>
    /// Returns whether a requester's reply to a solved ticket comes too late to reopen it, and starts a follow-up instead.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <param name="desk">Its desk, or <see langword="null"/>.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>Whether the reply starts a follow-up ticket.</returns>
    public static bool StartsFollowUp(TicketPart part, TicketDesk desk, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.StatusCategory == TicketStatusCategory.Closed
            || (part.StatusCategory == TicketStatusCategory.Solved && !ReopensOnRequesterReply(part, desk, nowUtc));
    }

    /// <summary>
    /// Returns whether a solved ticket has waited the desk's auto-close delay and closes on its own.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <param name="desk">Its desk, or <see langword="null"/>.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>Whether it closes.</returns>
    public static bool ShouldAutoClose(TicketPart part, TicketDesk desk, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.StatusCategory == TicketStatusCategory.Solved
            && desk is { AutoCloseDays: > 0 }
            && part.SolvedUtc is { } solved
            && solved.AddDays(desk.AutoCloseDays) <= nowUtc;
    }

    /// <summary>
    /// Picks the first status of a category, in the order the statuses are listed.
    /// </summary>
    /// <param name="statuses">The statuses.</param>
    /// <param name="category">The category.</param>
    /// <returns>The status, or <see langword="null"/> when the category has none.</returns>
    public static TicketStatus FirstOf(IEnumerable<TicketStatus> statuses, TicketStatusCategory category)
        => statuses?
            .Where(status => status.Category == category)
            .OrderBy(status => status.Order)
            .ThenBy(status => status.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
}

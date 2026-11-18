using System.Linq.Expressions;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The tickets a user may see, as one rule the list, the API, the report data sets and the reports all apply: every
/// ticket of a desk where the user sees all, the tickets of their teams (or assigned to them) where they see their
/// teams', and the tickets assigned to them where they see only those.
/// </summary>
public static class TicketScopeFilter
{
    // A value no id ever has, so an empty list matches nothing in an IN clause.
    private static readonly string[] _none = ["\u0000"];

    /// <summary>
    /// Gets the scope as a condition on <see cref="TicketIndex"/>.
    /// </summary>
    /// <param name="scope">The scope, from <see cref="Tickets.Services.ITicketAccessService.ApplyScopeAsync"/>.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<TicketIndex, bool>> ToPredicate(TicketScopedQuery scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var all = Values(scope.AllDeskIds);
        var team = Values(scope.TeamDeskIds);
        var assigned = Values(scope.AssignedDeskIds);
        var teams = Values(scope.TeamIds);
        var userId = string.IsNullOrEmpty(scope.UserId) ? _none[0] : scope.UserId;

        return index =>
            index.DeskId.IsIn(all)
            || (index.DeskId.IsIn(team) && (index.TeamId.IsIn(teams) || index.AssigneeId == userId))
            || (index.DeskId.IsIn(assigned) && index.AssigneeId == userId);
    }

    /// <summary>
    /// Gets whether the scope lets the user see a ticket of a desk, team and assignee.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="deskId">The ticket's desk.</param>
    /// <param name="teamId">The ticket's team.</param>
    /// <param name="assigneeId">The ticket's assignee.</param>
    /// <returns><see langword="true"/> when the user may see it.</returns>
    public static bool Allows(TicketScopedQuery scope, string deskId, string teamId, string assigneeId)
    {
        if (scope is null || string.IsNullOrEmpty(deskId))
        {
            return false;
        }

        var mine = !string.IsNullOrEmpty(scope.UserId) && string.Equals(assigneeId, scope.UserId, StringComparison.Ordinal);

        return scope.AllDeskIds.Contains(deskId, StringComparer.Ordinal)
            || (scope.TeamDeskIds.Contains(deskId, StringComparer.Ordinal) && (mine || (!string.IsNullOrEmpty(teamId) && scope.TeamIds.Contains(teamId, StringComparer.Ordinal))))
            || (scope.AssignedDeskIds.Contains(deskId, StringComparer.Ordinal) && mine);
    }

    private static string[] Values(IReadOnlyList<string> values)
        => values is { Count: > 0 } ? [.. values] : _none;
}

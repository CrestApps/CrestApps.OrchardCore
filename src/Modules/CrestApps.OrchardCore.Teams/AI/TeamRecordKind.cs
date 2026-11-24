using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Teams.AI;

/// <summary>
/// The parts of the <c>team</c> record kind that differ from the team manager's defaults: the allow-listed fields, the
/// list in tree order as Content › Teams shows it, and the check the Delete button runs first.
/// </summary>
internal static class TeamRecordKind
{
    /// <summary>
    /// The name of the kind.
    /// </summary>
    public const string Name = "team";

    /// <summary>
    /// Describes a team from an explicit list of its fields, with the postal address its editor's Address card shows (the
    /// address the Workforce time clock geofence is measured from). The settings other modules keep on a team (its
    /// <c>Properties</c>, such as the Workforce settings) are left out.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <returns>The fields the record tools return.</returns>
    public static object Project(Team team)
        => new
        {
            id = team.ItemId,
            name = team.Name,
            kind = team.Kind.ToString(),
            parentId = team.ParentId,
            description = team.Description,
            linkedRoleName = team.LinkedRoleName,
            timeZoneId = team.TimeZoneId,
            address = team.Address is null ? null : AddressArguments.Describe(team.Address),
            enabled = team.Enabled,
        };

    /// <summary>
    /// Lists the teams as the Teams page does: each team followed by its sub-teams, filtered by text in the name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="query">The page and the text to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of teams and how many match.</returns>
    public static async ValueTask<PageResult<Team>> ListAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var text = query.Text?.Trim();
        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken))
            .Select(item => item.Team)
            .Where(team => string.IsNullOrEmpty(text) || (team.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);

        return new PageResult<Team>
        {
            Count = teams.Count,
            Entries = teams.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }

    /// <summary>
    /// Says why a team cannot be deleted, as the Teams page does before it deletes one: it still has sub-teams, or another
    /// module still depends on it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reasons, or <see langword="null"/> when the team can be deleted.</returns>
    public static async ValueTask<string> GetUseAsync(IServiceProvider services, Team team, CancellationToken cancellationToken)
    {
        var blockers = await services.GetRequiredService<ITeamManager>().GetDeletionBlockersAsync(team, cancellationToken);

        return blockers.Count == 0
            ? null
            : string.Join(" ", blockers);
    }
}

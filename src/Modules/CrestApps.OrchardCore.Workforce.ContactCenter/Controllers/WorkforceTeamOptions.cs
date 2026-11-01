using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// Builds team pickers.
/// </summary>
internal static class WorkforceTeamOptions
{
    /// <summary>
    /// Gets the teams in a scope as options, indented by depth.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="selected">The selected teams.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options.</returns>
    public static async Task<IList<SelectListItem>> ListAsync(ITeamManager teamManager, WorkforceScope scope, IEnumerable<string> selected, CancellationToken cancellationToken)
    {
        var chosen = selected?.ToHashSet(StringComparer.Ordinal) ?? [];

        return (await teamManager.ListTreeAsync(cancellationToken))
            .Where(item => scope.Covers(item.Team.ItemId))
            .Select(item => new SelectListItem(new string('\u00A0', item.Depth * 4) + item.Team.Name, item.Team.ItemId, chosen.Contains(item.Team.ItemId)))
            .ToList();
    }
}

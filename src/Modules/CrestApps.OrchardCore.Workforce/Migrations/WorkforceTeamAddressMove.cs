using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Moves the one-line address Workforce kept on a team (<c>WorkforceTeamSettings.Location.Address</c>) onto the
/// team's own structured address, as its first line, when the team has none yet.
/// </summary>
internal static class WorkforceTeamAddressMove
{
    public static async Task<int> MoveAsync(ITeamManager teamManager, CancellationToken cancellationToken = default)
    {
        var moved = 0;

        foreach (var team in await teamManager.GetAllAsync(cancellationToken))
        {
            if (!Lift(team))
            {
                continue;
            }

            await teamManager.UpdateAsync(team, null, cancellationToken);
            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Lifts the legacy line onto the team and drops it from the Workforce settings.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <returns><see langword="true"/> when the team changed.</returns>
    public static bool Lift(Team team)
    {
        if (!team.Properties.TryGetValue(nameof(WorkforceTeamSettings), out var value) || value is null)
        {
            return false;
        }

        var settings = value as JsonObject ?? JsonSerializer.SerializeToNode(value) as JsonObject;

        if (settings?["Location"] is not JsonObject location || location["Address"] is not JsonValue legacy || !legacy.TryGetValue<string>(out var line))
        {
            return false;
        }

        if (team.Address is null && !string.IsNullOrWhiteSpace(line))
        {
            team.Address = new Address { AddressLine1 = line.Trim() };
        }

        location.Remove("Address");

        // A location that only held the address keeps nothing.
        if (location.Count == 0 || (location["Latitude"] is null && location["Longitude"] is null))
        {
            settings.Remove("Location");
        }

        team.Properties[nameof(WorkforceTeamSettings)] = settings;

        return true;
    }
}

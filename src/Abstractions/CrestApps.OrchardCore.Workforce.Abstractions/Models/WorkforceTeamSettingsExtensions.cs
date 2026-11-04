using CrestApps.Core;
using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Reads and changes the <see cref="WorkforceTeamSettings"/> stored on a team.
/// </summary>
public static class WorkforceTeamSettingsExtensions
{
    /// <summary>
    /// Gets the team's Workforce settings. The settings are attached to the team, so later calls return the same
    /// object, and changes made to it are saved with the team.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <returns>The settings; empty when the team has none yet.</returns>
    public static WorkforceTeamSettings GetWorkforceSettings(this Team team)
    {
        ArgumentNullException.ThrowIfNull(team);

        if (team.Properties.TryGetValue(nameof(WorkforceTeamSettings), out var value) && value is WorkforceTeamSettings attached)
        {
            return attached;
        }

        var settings = team.GetOrCreate<WorkforceTeamSettings>();

        team.Put(settings);

        return settings;
    }

    /// <summary>
    /// Changes the team's Workforce settings.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <param name="alter">Changes the settings.</param>
    /// <returns>The team.</returns>
    public static Team AlterWorkforceSettings(this Team team, Action<WorkforceTeamSettings> alter)
    {
        ArgumentNullException.ThrowIfNull(alter);

        alter(team.GetWorkforceSettings());

        return team;
    }
}

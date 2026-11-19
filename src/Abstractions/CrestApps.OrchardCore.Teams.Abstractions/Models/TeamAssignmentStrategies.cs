namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// The built-in ways <see cref="Services.ITeamAssignmentService"/> picks a member of a team for a piece of work.
/// </summary>
public static class TeamAssignmentStrategies
{
    /// <summary>
    /// The member who was given work of this kind longest ago, so work goes around the team in turn.
    /// </summary>
    public const string RoundRobin = "RoundRobin";

    /// <summary>
    /// The member with the least open work of this kind.
    /// </summary>
    public const string LeastLoaded = "LeastLoaded";
}

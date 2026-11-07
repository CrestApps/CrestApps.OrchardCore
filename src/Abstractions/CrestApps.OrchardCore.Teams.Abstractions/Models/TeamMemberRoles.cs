namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// The well-known values of <see cref="TeamMember.Role"/>. The role is an open string, so a provider can give others.
/// </summary>
public static class TeamMemberRoles
{
    /// <summary>
    /// Works in the team.
    /// </summary>
    public const string Member = "Member";

    /// <summary>
    /// Leads the team or crew on the job.
    /// </summary>
    public const string Lead = "Lead";

    /// <summary>
    /// Plans the team's schedule.
    /// </summary>
    public const string Scheduler = "Scheduler";

    /// <summary>
    /// Manages the team and its sub-teams.
    /// </summary>
    public const string Manager = "Manager";
}

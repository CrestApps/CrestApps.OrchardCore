namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a worker does in a team.
/// </summary>
public enum TeamMemberRole
{
    /// <summary>
    /// Works in the team.
    /// </summary>
    Member,

    /// <summary>
    /// Leads the team or crew on the job.
    /// </summary>
    Lead,

    /// <summary>
    /// Plans the team's schedule.
    /// </summary>
    Scheduler,

    /// <summary>
    /// Manages the team and its sub-teams: approves their requests and sees their details.
    /// </summary>
    Manager,
}

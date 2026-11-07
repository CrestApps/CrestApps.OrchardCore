namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The workers on a team today, shown on the team editor.
/// </summary>
public class WorkforceTeamMembersViewModel
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the workers and what they do in the team.
    /// </summary>
    public IList<WorkforceTeamMemberRow> Members { get; set; } = [];
}

/// <summary>
/// One worker on a team.
/// </summary>
/// <param name="WorkerId">The worker.</param>
/// <param name="DisplayName">The worker's name.</param>
/// <param name="Role">What the worker does in the team.</param>
/// <param name="HasUser">Whether the worker signs in, and so counts as a team member for calendars and other modules.</param>
public sealed record WorkforceTeamMemberRow(string WorkerId, string DisplayName, string Role, bool HasUser);

namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// How much work of one kind a team member has now, as the module that keeps that work reports it.
/// </summary>
public sealed class TeamMemberLoad
{
    /// <summary>
    /// Gets or sets how many open items of the kind are assigned to the member.
    /// </summary>
    public int Open { get; set; }

    /// <summary>
    /// Gets or sets when an item of the kind was last assigned to the member, in UTC, or <see langword="null"/> when
    /// none ever was.
    /// </summary>
    public DateTime? LastAssignedUtc { get; set; }
}

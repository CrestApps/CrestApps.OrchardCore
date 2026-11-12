namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// The role a staff member has in a team.
/// </summary>
public enum TeamMemberRole
{
    /// <summary>
    /// A member.
    /// </summary>
    Member,

    /// <summary>
    /// A team lead.
    /// </summary>
    Lead,

    /// <summary>
    /// Plans the team's work.
    /// </summary>
    Scheduler,

    /// <summary>
    /// Manages the team and its sub-teams.
    /// </summary>
    Manager,
}

/// <summary>
/// A staff member's membership in a team, with its dates.
/// </summary>
public sealed class TeamMembership
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the role in the team.
    /// </summary>
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

    /// <summary>
    /// Gets or sets the first day of the membership, or <see langword="null"/> for always.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day of the membership, or <see langword="null"/> for open-ended.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets whether the membership counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    public bool IsEffectiveOn(DateOnly date)
    {
        return (EffectiveFrom is null || EffectiveFrom <= date) && (EffectiveTo is null || EffectiveTo >= date);
    }
}

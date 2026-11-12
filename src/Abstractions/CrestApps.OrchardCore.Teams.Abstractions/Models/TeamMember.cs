namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// A person's place in a team, from one day to another. Teams keeps no member list of its own: the modules that know who
/// belongs to a team (Workforce workers, for one) give their members through an
/// <see cref="Services.ITeamMemberProvider"/>, and every module reads them through
/// <see cref="Services.ITeamMembershipService"/>.
/// </summary>
public sealed class TeamMember
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the member's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the staff member, when the provider keeps staff; people without a user account have only this.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets what the member does in the team, an open value such as one of <see cref="TeamMemberRoles"/>.
    /// </summary>
    public string Role { get; set; } = TeamMemberRoles.Member;

    /// <summary>
    /// Gets or sets the first day of the membership, or <see langword="null"/> for always.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day of the membership, or <see langword="null"/> when it doesn't end.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets the provider that gave the membership, such as <c>Workforce</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets whether the membership counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when the day is inside its dates.</returns>
    public bool IsEffectiveOn(DateOnly date)
        => (EffectiveFrom is null || EffectiveFrom <= date) && (EffectiveTo is null || EffectiveTo >= date);
}

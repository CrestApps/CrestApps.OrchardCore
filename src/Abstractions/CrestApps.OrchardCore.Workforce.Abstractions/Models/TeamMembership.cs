namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's place in a team, from one day to another.
/// </summary>
public sealed class TeamMembership
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets what the worker does in the team.
    /// </summary>
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

    /// <summary>
    /// Gets or sets the first day of the membership, or <see langword="null"/> for always.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day of the membership, or <see langword="null"/> when it doesn't end.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets whether the membership counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when the day is inside its dates.</returns>
    public bool IsEffectiveOn(DateOnly date)
    {
        return (EffectiveFrom is null || EffectiveFrom <= date) && (EffectiveTo is null || EffectiveTo >= date);
    }
}

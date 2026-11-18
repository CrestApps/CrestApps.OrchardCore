namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// Who team assignment picked, or why it picked nobody.
/// </summary>
/// <param name="Member">The member picked, or <see langword="null"/>.</param>
/// <param name="Outcome">Why.</param>
public sealed record TeamAssignmentResult(TeamMember Member, TeamAssignmentOutcome Outcome)
{
    /// <summary>
    /// Gets whether a member was picked.
    /// </summary>
    public bool Picked => Member is not null;
}

/// <summary>
/// Why team assignment picked the member it did, or nobody.
/// </summary>
public enum TeamAssignmentOutcome
{
    /// <summary>
    /// A member was picked.
    /// </summary>
    Picked,

    /// <summary>
    /// No team was given.
    /// </summary>
    NoTeam,

    /// <summary>
    /// The team has no member with a login (or every one was excluded).
    /// </summary>
    NoMembers,

    /// <summary>
    /// Every member was reported unavailable now (off shift, absent, signed out).
    /// </summary>
    NoneAvailable,

    /// <summary>
    /// No available member passed the caller's filter (such as holding the required skills).
    /// </summary>
    NoneQualified,

    /// <summary>
    /// Every qualified member already holds the most open work allowed.
    /// </summary>
    AllAtCapacity,
}

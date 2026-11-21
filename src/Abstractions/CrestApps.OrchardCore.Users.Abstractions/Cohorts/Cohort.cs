namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A cohort: a named group of people kept by hand, such as "2026 new hires" or "Warehouse supervisors". Unlike an
/// audience, whose rules decide who is in it, a cohort holds exactly the people someone added. Other features target
/// it through the "Cohort member" audience rule, or link to it directly, such as enrollment by cohort in Learning.
/// </summary>
public sealed class Cohort
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name, unique among cohorts without regard to case.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets when the cohort was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the cohort was last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// One person's place in a cohort.
/// </summary>
public sealed class CohortMember
{
    /// <summary>
    /// Gets or sets the cohort.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets when the person was added, in UTC.
    /// </summary>
    public DateTime AddedUtc { get; set; }

    /// <summary>
    /// Gets or sets who added the person: a user id or the name of a process, such as a recipe.
    /// </summary>
    public string AddedBy { get; set; }
}

/// <summary>
/// The outcome of a change to a cohort.
/// </summary>
/// <param name="Cohort">The cohort, or <see langword="null"/> when the change was refused.</param>
/// <param name="Errors">Why the change was refused; empty when it was made.</param>
public sealed record CohortResult(Cohort Cohort, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}

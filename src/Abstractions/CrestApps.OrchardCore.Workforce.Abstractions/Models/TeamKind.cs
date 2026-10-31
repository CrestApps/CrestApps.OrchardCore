namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What level of the organization a team is.
/// </summary>
public enum TeamKind
{
    /// <summary>
    /// The whole organization.
    /// </summary>
    Organization,

    /// <summary>
    /// A region.
    /// </summary>
    Region,

    /// <summary>
    /// A site or location, such as a contact center building or a depot.
    /// </summary>
    Site,

    /// <summary>
    /// A department.
    /// </summary>
    Department,

    /// <summary>
    /// A team.
    /// </summary>
    Team,

    /// <summary>
    /// A crew sent out as one unit for field work.
    /// </summary>
    Crew,
}

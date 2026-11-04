using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Core.Indexes;

/// <summary>
/// The YesSql index of teams.
/// </summary>
public sealed class TeamIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the team name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what level of the organization the team is.
    /// </summary>
    public TeamKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the parent team.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets whether the team is in use.
    /// </summary>
    public bool Enabled { get; set; }
}

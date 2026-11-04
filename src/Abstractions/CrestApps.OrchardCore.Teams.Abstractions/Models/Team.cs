using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// A part of the organization: a region, a site, a department, a team or a crew. Teams nest; a manager of a team also
/// manages its sub-teams. Other modules keep their own settings for a team in its <see cref="ExtensibleEntity.Properties"/>.
/// </summary>
public sealed class Team : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the team name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what level of the organization the team is.
    /// </summary>
    public TeamKind Kind { get; set; } = TeamKind.Team;

    /// <summary>
    /// Gets or sets the team it belongs to, or <see langword="null"/> at the top.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets what the team does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a role whose members are treated as part of the team, such as for reading its calendar.
    /// </summary>
    public string LinkedRoleName { get; set; }

    /// <summary>
    /// Gets or sets the team's IANA time zone, which sets its day boundaries; <see langword="null"/> to use the site's.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the team is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the team was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the team was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

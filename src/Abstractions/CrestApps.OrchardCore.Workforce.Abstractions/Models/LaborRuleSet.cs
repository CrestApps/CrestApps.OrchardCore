using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Labor rules combined for a team, a location or a jurisdiction, and who they apply to. A worker gets the set assigned to them alone, or else the nearest team's.
/// </summary>
public sealed class LaborRuleSet : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the set is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the place or law it follows, as free text.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public IList<LaborRuleInstance> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams it applies to, with their sub-teams.
    /// </summary>
    public IList<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers it applies to alone, which takes precedence over their teams' set.
    /// </summary>
    public IList<string> WorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the employment types it applies to; empty for every type.
    /// </summary>
    public IList<EmploymentType> EmploymentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

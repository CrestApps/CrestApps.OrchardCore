using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A reusable shift: when it starts, how long it lasts and the breaks inside it.
/// </summary>
public sealed class ShiftTemplate : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color shifts of the template are drawn in.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the wall-clock time the shift starts, in the team's time zone.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Gets or sets how long the shift lasts, breaks included.
    /// </summary>
    public int LengthMinutes { get; set; } = 480;

    /// <summary>
    /// Gets or sets the breaks, meals and other activities inside the shift.
    /// </summary>
    public IList<ShiftTemplateSegment> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams that use the template; empty for every team.
    /// </summary>
    public IList<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills a worker needs to work a shift of the template.
    /// </summary>
    public IList<string> RequiredSkillIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the template can be used.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

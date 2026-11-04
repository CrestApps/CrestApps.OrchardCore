using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// Something a person can do, such as speaking Spanish or servicing a boiler, that work can require.
/// </summary>
public sealed class Skill : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique skill name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a free-text group the skill is listed under, such as Languages or Electrical.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets what the skill means.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the certification a person needs, valid on the day, before the skill counts for them; or
    /// <see langword="null"/> when holding the skill is enough.
    /// </summary>
    public string RequiredCertificationId { get; set; }

    /// <summary>
    /// Gets or sets whether the skill can be given to people and required by work.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the skill was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the skill was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a routeable Contact Center capability that can be assigned to agents and required by queues.
/// </summary>
public sealed class ContactCenterSkill : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique skill name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the skill description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skill can be selected by agents and queues.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the skill was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the skill was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

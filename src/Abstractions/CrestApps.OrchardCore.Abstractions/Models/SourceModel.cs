using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Models;

public abstract class SourceModel : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the profile.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets the name of the source for this profile.
    /// </summary>
    public string Source { get; set; }
}

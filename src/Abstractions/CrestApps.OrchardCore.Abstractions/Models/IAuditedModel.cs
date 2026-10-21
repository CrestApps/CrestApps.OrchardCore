using CrestApps.Core;

namespace CrestApps.OrchardCore.Models;

/// <summary>
/// Represents a stored record that remembers when it was created and changed, and by whom.
/// </summary>
/// <remarks>
/// These members belong to the environment that stores the record, not to its configuration: a deployment plan never
/// carries them, and the environment that receives the record stamps its own.
/// </remarks>
public interface IAuditedModel : IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the UTC date the record was created.
    /// </summary>
    DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the record.
    /// </summary>
    string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user that created the record.
    /// </summary>
    string Author { get; set; }
}

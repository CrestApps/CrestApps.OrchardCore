namespace CrestApps.OrchardCore.Models;

/// <summary>
/// Represents a stored record that remembers when it was first saved and when it was last saved.
/// </summary>
/// <remarks>
/// A store built on the YesSql <c>DocumentCatalog</c> base with a clock stamps both members on every save, so the
/// record's own code never sets them.
/// </remarks>
public interface ITimestampedModel
{
    /// <summary>
    /// Gets or sets the UTC date the record was first saved.
    /// </summary>
    DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the record was last saved.
    /// </summary>
    DateTime UpdatedUtc { get; set; }
}

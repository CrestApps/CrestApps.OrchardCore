namespace CrestApps.Core.AI.Memory;

/// <summary>
/// Represents the AI memory settings.
/// </summary>
public sealed class AIMemorySettings
{
    /// <summary>
    /// Gets or sets the index profile name.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the top n.
    /// </summary>
    public int TopN { get; set; } = 5;
}

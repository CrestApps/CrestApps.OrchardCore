namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Marks an AI profile made from an earlier version of another, to run that version beside the current one.
/// </summary>
public sealed class AIProfileVersionCopy
{
    /// <summary>
    /// Gets or sets the profile the version belongs to.
    /// </summary>
    public string SourceProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version the copy runs.
    /// </summary>
    public int Version { get; set; }
}

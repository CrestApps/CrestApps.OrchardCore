namespace CrestApps.Core.AI.A2A.Models;

/// <summary>
/// Stores the selected A2A connection IDs on an AI profile, template, or chat interaction.
/// </summary>
public sealed class AIProfileA2AMetadata
{
    public string[] ConnectionIds { get; set; }
}

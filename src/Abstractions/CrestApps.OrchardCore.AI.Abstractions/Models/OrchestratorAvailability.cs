namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents whether an orchestrator is currently available for use.
/// </summary>
public sealed class OrchestratorAvailability
{
    /// <summary>
    /// Gets or sets a value indicating whether the orchestrator is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets the message explaining why the orchestrator is unavailable.
    /// </summary>
    public string Message { get; set; }
}

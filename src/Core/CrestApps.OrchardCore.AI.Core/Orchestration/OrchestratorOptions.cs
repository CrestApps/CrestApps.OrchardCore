namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Configuration for registered orchestrators.
/// </summary>
public sealed class OrchestratorOptions
{
    /// <summary>
    /// Gets or sets the default orchestrator name.
    /// When a profile or interaction does not specify an orchestrator, this name is used.
    /// </summary>
    public string DefaultOrchestratorName { get; set; } = ProgressiveToolOrchestrator.OrchestratorName;

    internal Dictionary<string, Type> Orchestrators { get; } = new(StringComparer.OrdinalIgnoreCase);
}

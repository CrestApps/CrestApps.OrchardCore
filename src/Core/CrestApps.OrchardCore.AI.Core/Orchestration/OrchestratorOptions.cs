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
    public string DefaultOrchestratorName { get; set; } = DefaultOrchestrator.OrchestratorName;

    internal Dictionary<string, OrchestratorEntry> Orchestrators { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered orchestrators as a read-only collection of name/title pairs.
    /// </summary>
    public IReadOnlyDictionary<string, OrchestratorDescriptor> GetOrchestratorDescriptors()
    {
        return Orchestrators.ToDictionary(
            kvp => kvp.Key,
            kvp => new OrchestratorDescriptor { Title = kvp.Value.Title },
            StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Describes a registered orchestrator including its implementation type and optional display title.
/// </summary>
public sealed class OrchestratorEntry
{
    /// <summary>
    /// Gets or sets the orchestrator implementation type.
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// Gets or sets the optional localized display title for this orchestrator.
    /// When <see langword="null"/> or empty, the orchestrator name is used in the UI.
    /// </summary>
    public string Title { get; set; }
}

/// <summary>
/// Public descriptor for a registered orchestrator, exposing only metadata.
/// </summary>
public sealed class OrchestratorDescriptor
{
    /// <summary>
    /// Gets or sets the display title. When <see langword="null"/>, the name should be used.
    /// </summary>
    public string Title { get; set; }
}

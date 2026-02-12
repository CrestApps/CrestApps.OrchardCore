namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Resolves the appropriate <see cref="IOrchestrator"/> for a chat session based on
/// the configured orchestrator name.
/// </summary>
/// <remarks>
/// Resolution order: explicit name → system default.
/// If no name is specified or the named orchestrator is not found, the system default is returned.
/// </remarks>
public interface IOrchestratorResolver
{
    /// <summary>
    /// Resolves an orchestrator by name. Returns the system default if the name is
    /// <see langword="null"/>, empty, or unrecognized.
    /// </summary>
    /// <param name="orchestratorName">The orchestrator name, or <see langword="null"/> for the default.</param>
    /// <returns>The resolved <see cref="IOrchestrator"/> instance.</returns>
    IOrchestrator Resolve(string orchestratorName = null);
}

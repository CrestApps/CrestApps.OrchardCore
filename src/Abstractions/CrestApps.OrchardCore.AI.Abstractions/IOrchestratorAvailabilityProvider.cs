using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides availability state for a specific orchestrator so callers can block
/// requests before attempting to execute an unavailable orchestrator.
/// </summary>
public interface IOrchestratorAvailabilityProvider
{
    /// <summary>
    /// Gets the orchestrator name this provider applies to.
    /// </summary>
    string OrchestratorName { get; }

    /// <summary>
    /// Gets the current availability state for the orchestrator.
    /// </summary>
    Task<OrchestratorAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);
}

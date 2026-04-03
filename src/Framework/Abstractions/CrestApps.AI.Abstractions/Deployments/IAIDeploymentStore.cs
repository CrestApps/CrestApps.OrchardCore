using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI.Deployments;

/// <summary>
/// Represents the host-specific persisted AI deployment catalog before any configuration-backed
/// deployments are merged into read operations.
/// </summary>
public interface IAIDeploymentStore : INamedSourceCatalog<AIDeployment>
{
}

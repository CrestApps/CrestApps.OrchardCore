using Microsoft.Extensions.Localization;

namespace CrestApps.Core.AI;

public sealed class AIDeploymentProviderEntry
{
    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }

    /// <summary>
    /// When <c>true</c>, deployments under this provider carry their own connection
    /// parameters (endpoint, credentials) in <see cref="AI.Models.AIDeployment.Properties"/>
    /// instead of referencing a shared <c>AIProviderConnection</c>.
    /// </summary>
    public bool SupportsContainedConnection { get; set; }
}

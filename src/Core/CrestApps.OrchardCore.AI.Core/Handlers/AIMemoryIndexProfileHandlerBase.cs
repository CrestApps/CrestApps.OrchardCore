using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// The base of an index profile handler for the AI memory index of one provider.
/// </summary>
public abstract class AIMemoryIndexProfileHandlerBase : EmbeddingIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="providerName">The name of the indexing provider the handler manages.</param>
    /// <param name="deploymentManager">The deployment manager for resolving AI deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="logger">The logger the embedding failures are written to; nothing is logged when omitted.</param>
    protected AIMemoryIndexProfileHandlerBase(
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger logger = null)
        : base(MemoryConstants.IndexingTaskType, providerName, deploymentManager, aiClientFactory, logger)
    {
    }
}

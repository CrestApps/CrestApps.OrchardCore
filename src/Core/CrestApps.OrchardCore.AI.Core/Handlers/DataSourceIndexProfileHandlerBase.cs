using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// The base of an index profile handler for the data source knowledge base index of one provider.
/// </summary>
public abstract class DataSourceIndexProfileHandlerBase : EmbeddingIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="providerName">The name of the indexing provider the handler manages.</param>
    /// <param name="deploymentManager">The deployment manager for resolving AI deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="logger">The logger instance.</param>
    protected DataSourceIndexProfileHandlerBase(
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger logger)
        : base(DataSourceConstants.IndexingTaskType, providerName, deploymentManager, aiClientFactory, logger)
    {
    }
}

using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;

/// <summary>
/// Base class for index profile handlers that manage AI document indexing operations.
/// </summary>
public abstract class AIDocumentIndexProfileHandlerBase : EmbeddingIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="providerName">The name of the indexing provider this handler manages.</param>
    /// <param name="deploymentManager">The deployment manager for resolving AI deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="logger">The logger instance.</param>
    protected AIDocumentIndexProfileHandlerBase(
        string providerName,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger logger)
        : base(AIConstants.AIDocumentsIndexingTaskType, providerName, deploymentManager, aiClientFactory, logger)
    {
    }
}

using CrestApps.AI.Clients;
using CrestApps.AI.Completions;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.Templates.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class NamedAICompletionClient : CrestApps.AI.Services.NamedAICompletionClient
{
    protected NamedAICompletionClient(
        string name,
        IAIClientFactory aIClientFactory,
        IDistributedCache distributedCache,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        ITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager)
    : base(
        name,
        aIClientFactory,
        distributedCache,
        loggerFactory,
        serviceProvider,
        providerOptions,
        defaultOptions,
        handlers,
        aiTemplateService,
        deploymentManager)
    {
    }
}

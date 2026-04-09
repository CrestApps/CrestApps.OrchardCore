using CrestApps.Core.AI.AzureAIInference;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionClient : DeploymentAwareAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IServiceProvider serviceProvider,
        DefaultAIOptions defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        ITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager
        ) : base(
        AzureAIInferenceConstants.ImplementationName,
        aIClientFactory,
        distributedCache,
        loggerFactory,
        serviceProvider,
        providerOptions.Value,
        defaultOptions,
        handlers,
        deploymentStore,
        aiTemplateService,
        deploymentManager)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceConstants.ClientName;
}

using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.AzureAIInference.Services;

public sealed class AzureAIInferenceCompletionClient : NamedAICompletionClient
{
    public AzureAIInferenceCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        ITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager)
    : base(
        AzureAIInferenceConstants.ImplementationName,
        aIClientFactory, distributedCache,
        loggerFactory,
        serviceProvider,
        providerOptions.Value,
        defaultOptions.Value,
        handlers,
        aiTemplateService,
        deploymentManager)
    {
    }

    protected override string ProviderName
        => AzureAIInferenceConstants.ProviderName;
}

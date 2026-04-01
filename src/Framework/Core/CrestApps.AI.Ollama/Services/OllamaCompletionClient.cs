using CrestApps.AI.Clients;
using CrestApps.AI.Completions;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Templates.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Ollama;

public sealed class OllamaCompletionClient : NamedAICompletionClient
{
    public OllamaCompletionClient(
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
        OllamaConstants.ImplementationName,
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
        => OllamaConstants.ProviderName;
}

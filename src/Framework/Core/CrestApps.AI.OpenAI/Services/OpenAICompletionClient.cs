using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.AI.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.OpenAI;

public sealed class OpenAICompletionClient : NamedAICompletionClient
{
    public OpenAICompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        IAITemplateService aiTemplateService)
        : base(
            OpenAIConstants.ImplementationName,
            aIClientFactory, distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions.Value,
            defaultOptions.Value,
            handlers,
            aiTemplateService)
    {
    }

    protected override string ProviderName
        => OpenAIConstants.ProviderName;
}

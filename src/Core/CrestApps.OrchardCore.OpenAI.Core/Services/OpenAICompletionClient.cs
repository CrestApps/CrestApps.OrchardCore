using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public class OpenAICompletionClient : DeploymentAwareAICompletionClient
{
    private readonly IEnumerable<IOpenAIChatOptionsConfiguration> _openAIChatOptionsConfigurations;

    public OpenAICompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IServiceProvider serviceProvider,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        IEnumerable<IOpenAIChatOptionsConfiguration> openAIChatOptionsConfigurations,
        IAITemplateService aiTemplateService
        ) : base(
            OpenAIConstants.ImplementationName,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions.Value,
            defaultOptions.Value,
            handlers,
            deploymentStore,
            aiTemplateService)
    {
        _openAIChatOptionsConfigurations = openAIChatOptionsConfigurations;
    }

    protected OpenAICompletionClient(
        string implementationName,
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        AIProviderOptions providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IServiceProvider serviceProvider,
        DefaultAIOptions defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        IEnumerable<IOpenAIChatOptionsConfiguration> openAIChatOptionsConfigurations,
        IAITemplateService aiTemplateService
        ) : base(
            implementationName,
            aIClientFactory,
            distributedCache,
            loggerFactory,
            serviceProvider,
            providerOptions,
            defaultOptions,
            handlers,
            deploymentStore,
            aiTemplateService)
    {
        _openAIChatOptionsConfigurations = openAIChatOptionsConfigurations;
    }

    protected override string ProviderName
        => OpenAIConstants.ProviderName;

    protected override async ValueTask ConfigureChatOptionsAsync(CompletionServiceConfigureContext configureContext)
    {
        foreach (var handler in _openAIChatOptionsConfigurations)
        {
            await handler.InitializeConfigurationAsync(configureContext);
        }

        configureContext.ChatOptions.RawRepresentationFactory = _ =>
        {
            var chatCompletionOptions = new ChatCompletionOptions();

            foreach (var handler in _openAIChatOptionsConfigurations)
            {
                handler.Configure(configureContext, chatCompletionOptions);
            }

            return chatCompletionOptions;
        };
    }
}

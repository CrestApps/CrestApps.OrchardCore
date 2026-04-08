using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAIConst = CrestApps.Core.AI.OpenAI.OpenAIConstants;

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
        DefaultAIOptions defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        IEnumerable<IOpenAIChatOptionsConfiguration> openAIChatOptionsConfigurations,
        ITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager
        ) : base(
        OpenAIConst.ImplementationName,
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
        ITemplateService aiTemplateService,
        IAIDeploymentManager deploymentManager
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
        aiTemplateService,
        deploymentManager)
    {
        _openAIChatOptionsConfigurations = openAIChatOptionsConfigurations;
    }

    protected override string ProviderName
        => OpenAIConstants.ClientName;

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

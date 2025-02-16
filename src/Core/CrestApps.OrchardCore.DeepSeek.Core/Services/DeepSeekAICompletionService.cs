using System.ClientModel;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekAICompletionService : DeploymentNamedAICompletionService
{
    public DeepSeekAICompletionService(
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IOptions<AIProviderOptions> providerOptions,
           IAIToolsService toolsService,
           IOptions<DefaultAIOptions> defaultOptions,
           IAIDeploymentStore deploymentStore
           ) : base(DeepSeekAIDeploymentProvider.ProviderName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName
        => DeepSeekAIDeploymentProvider.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string modelName)
    {
        var client = new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://api.deepseek.com/v1"),
        });

        return client.AsChatClient(modelName);
    }

    // The 'deepseek-reasoner' model does not support tool calling.
    protected override bool SupportFunctionInvocation(AICompletionContext context, string modelName)
    {
        return modelName != "deepseek-reasoner" && base.SupportFunctionInvocation(context, modelName);
    }
}

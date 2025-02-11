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

public sealed class DeepSeekAICompletionService : NamedAICompletionService
{
    private readonly IDistributedCache _distributedCache;

    public DeepSeekAICompletionService(
        IOptions<AIProviderOptions> providerOptions,
        IDistributedCache distributedCache,
        IAIToolsService toolsService,
        IOptions<DefaultAIOptions> defaultOptions,
        IAIDeploymentStore deploymentStore,
        ILogger<DeepSeekAICompletionService> logger)
        : base(DeepSeekAIDeploymentProvider.ProviderName, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore, logger)
    {
        _distributedCache = distributedCache;
    }

    protected override string ProviderName
        => DeepSeekAIDeploymentProvider.ProviderName;

    protected override void OnOptions(ChatOptions options, string modelName)
    {
        if (UseFunctions(modelName))
        {
            return;
        }

        options.Tools = null;
    }

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string modelName)
    {
        var client = new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://api.deepseek.com/v1"),
        });

        var builder = new ChatClientBuilder(client.AsChatClient(modelName))
            .UseDistributedCache(_distributedCache);

        if (UseFunctions(modelName))
        {
            builder.UseFunctionInvocation(null, (r) =>
            {
                // Set the maximum number of iterations per request to 1 to prevent infinite function calling.
                r.MaximumIterationsPerRequest = 1;
            });
        }

        return builder.Build();
    }

    // The 'deepseek-reasoner' model does not support tool calling.
    private static bool UseFunctions(string modelName)
        => modelName != "deepseek-reasoner";
}

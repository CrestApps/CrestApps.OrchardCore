using System.ClientModel;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekCloudChatCompletionService : IAIChatCompletionService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IAIToolsService _toolsService;
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly DefaultDeepSeekOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public DeepSeekCloudChatCompletionService(
        IOptions<AIProviderOptions> providerOptions,
        IDistributedCache distributedCache,
        IAIToolsService toolsService,
        IOptions<DefaultDeepSeekOptions> defaultOptions,
        IAIDeploymentStore deploymentStore,
        ILogger<DeepSeekCloudChatCompletionService> logger)
    {
        _distributedCache = distributedCache;
        _toolsService = toolsService;
        _deploymentStore = deploymentStore;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public string Name { get; } = DeepSeekAIDeploymentProvider.ProviderName;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        string deploymentName = null;

        if (_providerOptions.Providers.TryGetValue(DeepSeekAIDeploymentProvider.ProviderName, out var entry))
        {
            var connectionName = DeepSeekConstants.DefaultCloudConnectionName;
            deploymentName = entry.DefaultDeploymentName;

            var deployment = await GetDeploymentAsync(context);

            if (deployment is not null)
            {
                connectionName = deployment.ConnectionName;
                deploymentName = deployment.Name;
            }

            if (!string.IsNullOrEmpty(connectionName) && entry.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                connection = connectionProperties;
            }
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile.Id);

            return AIChatCompletionResponse.Empty;
        }

        var metadata = context.Profile.As<DeepSeekChatProfileMetadata>();

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(x.Text)).ToArray();

        var skip = GetTotalMessagesToSkip(chatMessages.Length, pastMessageCount);

        var prompts = chatMessages.Skip(skip).Take(pastMessageCount).ToList();

        try
        {
            var chatClient = GetChatClient(connection.GetApiKey(false), deploymentName);

            var chatOptions = GetChatOptions(context, metadata, deploymentName);

            var data = await chatClient.CompleteAsync(prompts, chatOptions, cancellationToken);

            if (data?.Choices is not null && data.FinishReason == ChatFinishReason.Stop)
            {
                return new AIChatCompletionResponse
                {
                    Choices = data.Choices.Select(x => new AIChatCompletionChoice()
                    {
                        Content = x.Text,
                    }),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while chatting with the OpenAI service.");
        }

        return AIChatCompletionResponse.Empty;
    }

    private IChatClient GetChatClient(string apiKey, string modelId)
    {
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://api.deepseek.com/v1"),
        });

        var builder = new ChatClientBuilder(client.AsChatClient(modelId))
            .UseDistributedCache(_distributedCache);

        if (modelId != "deepseek-reasoner")
        {
            // The 'deepseek-reasoner' model does not support tool calling.
            builder.UseFunctionInvocation(null, (r) =>
            {
                // Set the maximum number of iterations per request to 1 to prevent infinite function calling.
                r.MaximumIterationsPerRequest = 1;
            });
        }

        return builder.Build();
    }

    private ChatOptions GetChatOptions(AIChatCompletionContext context, DeepSeekChatProfileMetadata metadata, string modelName)
    {
        var chatOptions = new ChatOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokens = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        // The 'deepseek-reasoner' model does not support tool calling.
        if (!context.DisableTools && context.Profile.FunctionNames is not null &&
            context.Profile.FunctionNames.Length > 0 &&
            modelName != "deepseek-reasoner")
        {
            chatOptions.Tools = [];

            foreach (var functionName in context.Profile.FunctionNames)
            {
                var function = _toolsService.GetFunction(functionName);

                if (function is null)
                {
                    continue;
                }

                chatOptions.Tools.Add(function);
            }

            if (chatOptions.Tools.Count == 0)
            {
                chatOptions.Tools = null;
            }
        }

        return chatOptions;
    }

    private async Task<AIDeployment> GetDeploymentAsync(AIChatCompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }

    private static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }
}

using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Ollama.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Ollama.Services;

public sealed class OllamaAIChatCompletionService : IAIChatCompletionService
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IDistributedCache _distributedCache;
    private readonly IAIToolsService _toolsService;
    private readonly DefaultOpenAIOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public OllamaAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultOpenAIOptions> defaultOptions,
        ILogger<OllamaAIChatCompletionService> logger)
    {
        _deploymentStore = deploymentStore;
        _distributedCache = distributedCache;
        _toolsService = toolsService;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public string Name => OllamaProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        string deploymentName = null;

        if (_providerOptions.Providers.TryGetValue(OllamaProfileSource.Key, out var entry))
        {
            var connectionName = entry.DefaultConnectionName;
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

        var metadata = context.Profile.As<OllamaChatProfileMetadata>();

        var chatClient = GetChatClient(connection, deploymentName);

        var chatOptions = new ChatOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokens = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        if (!context.DisableTools && context.Profile.FunctionNames is not null)
        {
            chatOptions.ToolMode = ChatToolMode.Auto;
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
        }

        var prompts = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemMessage(context, metadata)),
        };

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(x.Text));

        var skip = GetTotalMessagesToSkip(chatMessages.Count(), pastMessageCount);

        prompts.AddRange(chatMessages.Skip(skip).Take(pastMessageCount));

        try
        {
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
            _logger.LogError(ex, "An error occurred while chatting with the Ollama service.");
        }

        return AIChatCompletionResponse.Empty;
    }

    private static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }

    private static string GetSystemMessage(AIChatCompletionContext context, OllamaChatProfileMetadata metadata)
    {
        var systemMessage = metadata.SystemMessage ?? string.Empty;

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }

    private async Task<AIDeployment> GetDeploymentAsync(AIChatCompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }

    private IChatClient GetChatClient(AIProviderConnection connection, string deploymentName)
    {
        var endpoint = new Uri(connection.GetStringValue("Endpoint"));

        var azureClient = new OllamaChatClient(endpoint, connection.GetDefaultDeploymentName());

        return new ChatClientBuilder(azureClient)
            .UseDistributedCache(_distributedCache)
            .UseFunctionInvocation(null, (options) =>
            {
                // Set the maximum number of iterations per request to 1 as a safe net to prevent infinite function calling.
                options.MaximumIterationsPerRequest = 1;
            }).Build();
    }
}

using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekCloudChatCompletionService : IAIChatCompletionService
{
    private readonly IAIToolsService _toolsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DefaultDeepSeekOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public DeepSeekCloudChatCompletionService(
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultDeepSeekOptions> defaultOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<DeepSeekCloudChatCompletionService> logger)
    {
        _toolsService = toolsService;
        _httpClientFactory = httpClientFactory;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public string Name
        => DeepSeekCloudChatProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        if (_providerOptions.Providers.TryGetValue(DeepSeekConstants.DeepSeekProviderName, out var entry))
        {
            var connectionName = "deepseek-cloud";

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
            var chatClient = new DeepSeekChatClient(_httpClientFactory, connection.GetModel(false));

            var chatOptions = GetChatOptions(context, metadata, connection);

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

    private ChatOptions GetChatOptions(AIChatCompletionContext context, DeepSeekChatProfileMetadata metadata, AIProviderConnection connection)
    {
        var chatOptions = new ChatOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokens = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
            AdditionalProperties = [],
        };

        chatOptions.AdditionalProperties.TryAdd("apiKey", connection.GetApiKey(false));

        if (!context.DisableTools && context.Profile.FunctionNames is not null && context.Profile.FunctionNames.Length > 0 && connection.GetModel(false) != "deepseek-reasoner")
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

    private static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }
}

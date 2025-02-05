using Azure.AI.OpenAI;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIChatCompletionService : IAIChatCompletionService
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IAIToolsService _toolsService;
    private readonly DefaultOpenAIOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public AzureOpenAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultOpenAIOptions> defaultOptions,
        ILogger<AzureOpenAIChatCompletionService> logger)
    {
        _deploymentStore = deploymentStore;
        _toolsService = toolsService;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public string Name { get; } = AzureProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        string deploymentName = null;

        if (_providerOptions.Providers.TryGetValue(AzureOpenAIConstants.AzureProviderName, out var entry))
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

        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

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

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(x.Text)).ToArray();

        var skip = GetTotalMessagesToSkip(chatMessages.Length, pastMessageCount);

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
            _logger.LogError(ex, "An error occurred while chatting with the OpenAI service.");
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

    private static string GetSystemMessage(AIChatCompletionContext context, OpenAIChatProfileMetadata metadata)
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

    private static IChatClient GetChatClient(AIProviderConnection connection, string deploymentName)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient
            .AsChatClient(deploymentName)
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
    }
}

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
    private readonly AIConnectionOptions _connectionOptions;
    private readonly ILogger _logger;

    public AzureOpenAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IOptions<AIConnectionOptions> connectionOptions,
        IAIToolsService toolsService,
        IOptions<DefaultOpenAIOptions> defaultOptions,
        ILogger<AzureOpenAIChatCompletionService> logger)
    {
        _deploymentStore = deploymentStore;
        _toolsService = toolsService;
        _defaultOptions = defaultOptions.Value;
        _connectionOptions = connectionOptions.Value;
        _logger = logger;
    }

    public string Name { get; } = AzureProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        var deployment = await GetDeploymentAsync(context);

        if (deployment is null)
        {
            _logger.LogWarning("Unable to initiate chat. The profile with ID '{ProfileId}' lacks a DeploymentId, and the fallback DeploymentId in the context is also not configured.", context.Profile.Id);

            return AIChatCompletionResponse.Empty;
        }

        AIConnectionEntry connection = null;

        if (_connectionOptions.Connections.TryGetValue(AzureOpenAIConstants.AzureDeploymentSourceName, out var connections))
        {
            connection = connections.FirstOrDefault(x => x.Name != null && x.Name.Equals(deployment.ConnectionName, StringComparison.OrdinalIgnoreCase));
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. The DeploymentId '{DeploymentId}' belongs to a connection that does not exists (i.e., '{ConnectionName}').", context.Profile.DeploymentId, deployment.ConnectionName);

            return AIChatCompletionResponse.Empty;
        }

        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

        var chatClient = GetChatClient(connection, deployment.Name);

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
            var data = await chatClient.CompleteAsync(prompts, chatOptions);

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
            var deployment = await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);

            if (deployment is not null)
            {
                return deployment;
            }
        }

        if (!string.IsNullOrEmpty(content.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.DeploymentId);
        }

        return null;
    }

    private static IChatClient GetChatClient(AIConnectionEntry connection, string deploymentName)
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

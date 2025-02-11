using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class NamedAICompletionService : IAICompletionService
{
    private readonly IAIToolsService _toolsService;
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public NamedAICompletionService(
        string name,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IAIToolsService toolsService,
        IAIDeploymentStore deploymentStore,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _toolsService = toolsService;
        _deploymentStore = deploymentStore;
        _defaultOptions = defaultOptions;
        _providerOptions = providerOptions;
        _logger = logger;
    }

    public string Name { get; }

    protected abstract string ProviderName { get; }

    protected virtual string GetDefaultConnectionName(AIProvider provider)
    {
        return provider.DefaultConnectionName;
    }

    protected virtual string GetDefaultDeploymentName(AIProvider provider)
    {
        return provider.DefaultDeploymentName;
    }

    protected virtual void OnOptions(ChatOptions options, string modelName)
    {
    }

    protected abstract IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string modelName);

    public async Task<ChatCompletion> ChatAsync(IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        string deploymentName = null;

        if (_providerOptions.Providers.TryGetValue(ProviderName, out var provider))
        {
            var connectionName = GetDefaultConnectionName(provider);

            deploymentName = GetDefaultDeploymentName(provider);

            var deployment = await GetDeploymentAsync(context);

            if (deployment is not null)
            {
                connectionName = deployment.ConnectionName;
                deploymentName = deployment.Name;
            }

            if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                connection = connectionProperties;
            }
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile.Id);

            return null;
        }

        var metadata = context.Profile?.As<AIProfileMetadata>();

        var pastMessageCount = metadata?.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(x.Text));

        var skip = GetTotalMessagesToSkip(chatMessages.Count(), pastMessageCount);

        var prompts = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemMessage(context, metadata))
        };

        try
        {
            var chatClient = GetChatClient(connection, context, deploymentName);

            var chatOptions = GetChatOptions(context, metadata);

            OnOptions(chatOptions, deploymentName);

            prompts.AddRange(chatMessages.Skip(skip).Take(pastMessageCount));

            return await chatClient.CompleteAsync(prompts, chatOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while chatting with the {Name} service.", Name);
        }

        return null;
    }

    private ChatOptions GetChatOptions(AICompletionContext context, AIProfileMetadata metadata)
    {
        var chatOptions = new ChatOptions()
        {
            Temperature = metadata?.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata?.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata?.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata?.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokens = metadata?.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        if (!context.DisableTools && context.Profile?.FunctionNames is not null &&
            context.Profile?.FunctionNames.Length > 0)
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

    private async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile?.DeploymentId))
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

    private static string GetSystemMessage(AICompletionContext context, AIProfileMetadata metadata)
    {
        var systemMessage = string.Empty;

        if (!string.IsNullOrEmpty(context.SystemMessage))
        {
            systemMessage = context.SystemMessage;
        }
        else if (!string.IsNullOrEmpty(metadata?.SystemMessage))
        {
            systemMessage = metadata.SystemMessage;
        }

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }
}

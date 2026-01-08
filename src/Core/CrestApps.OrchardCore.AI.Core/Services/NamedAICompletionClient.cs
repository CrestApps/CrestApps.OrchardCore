using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class NamedAICompletionClient : AICompletionServiceBase, IAICompletionClient
{
    public const string DefaultLogCategory = "AICompletionService";

    private readonly IAIClientFactory _aIClientFactory;
    private readonly IDistributedCache _distributedCache;
    private readonly IEnumerable<IAICompletionServiceHandler> _handlers;
    private readonly DefaultAIOptions _defaultOptions;

    protected readonly ILogger Logger;
    protected readonly ILoggerFactory LoggerFactory;

    public NamedAICompletionClient(
        string name,
        IAIClientFactory aIClientFactory,
        IDistributedCache distributedCache,
        ILoggerFactory loggerFactory,
        AIProviderOptions providerOptions,
        DefaultAIOptions defaultOptions,
        IEnumerable<IAICompletionServiceHandler> handlers)
        : base(providerOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _aIClientFactory = aIClientFactory;
        _distributedCache = distributedCache;
        LoggerFactory = loggerFactory;
        _defaultOptions = defaultOptions;
        Logger = loggerFactory.CreateLogger(DefaultLogCategory);
        _handlers = handlers;
    }

    public string Name { get; }

    protected abstract string ProviderName { get; }

    [Obsolete("This method is obsolete and will be removed in future releases. Please use ConfigureChatOptionsAsync instead")]
    protected virtual void ConfigureChatOptions(ChatOptions options, string modelName)
    {
    }

    protected virtual ValueTask ConfigureChatOptionsAsync(CompletionServiceConfigureContext configureContext)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void ConfigureFunctionInvocation(FunctionInvokingChatClient client)
    {
        client.MaximumIterationsPerRequest = _defaultOptions.MaximumIterationsPerRequest;
    }

    protected virtual bool SupportFunctionInvocation(AICompletionContext context, string modelName)
    {
        return !context.DisableTools;
    }

    protected virtual void ConfigureLogger(LoggingChatClient client)
    {
    }

    protected virtual void ConfigureOpenTelemetry(OpenTelemetryChatClient client)
    {
    }

    protected virtual void ProcessChatResponseUpdate(ChatResponseUpdate update, IEnumerable<ChatMessage> prompts)
    {
    }

    protected virtual void ProcessChatResponse(ChatResponse response, IEnumerable<ChatMessage> prompts)
    {
    }

    public async Task<ChatResponse> CompleteAsync(IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        if (!ProviderOptions.Providers.TryGetValue(ProviderName, out var provider))
        {
            throw new ArgumentException($"Provider '{ProviderName}' not found.");
        }

        var connectionName = GetDefaultConnectionName(provider, context.ConnectionName);

        if (string.IsNullOrEmpty(connectionName))
        {
            Logger.LogWarning("Unable to chat. Unable to find a connection '{ConnectionName}' or the default connection", context.ConnectionName);

            return null;
        }

        var deploymentName = GetDefaultDeploymentName(provider, connectionName);

        if (string.IsNullOrEmpty(deploymentName))
        {
            Logger.LogWarning("Unable to chat. Unable to find a deployment id '{DeploymentId}' or the default deployment", context.DeploymentId);

            return null;
        }

        try
        {
            var chatClient = await BuildClientAsync(connectionName, context, deploymentName);

            var prompts = GetPrompts(messages, context);

            var chatOptions = await GetChatOptionsAsync(context, deploymentName, false);

            var response = await chatClient.GetResponseAsync(prompts, chatOptions, cancellationToken);

            ProcessChatResponse(response, prompts);

            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while chatting with the {Name} service.", Name);
        }

        return null;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(IEnumerable<ChatMessage> messages, AICompletionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        if (!ProviderOptions.Providers.TryGetValue(ProviderName, out var provider))
        {
            throw new ArgumentException($"Provider '{ProviderName}' not found.");
        }

        var connectionName = GetDefaultConnectionName(provider, context.ConnectionName);

        if (string.IsNullOrEmpty(connectionName))
        {
            Logger.LogWarning("Unable to chat. Unable to find a connection '{ConnectionName}' or the default connection", context.ConnectionName);

            yield break;
        }

        var deploymentName = GetDefaultDeploymentName(provider, connectionName);

        if (string.IsNullOrEmpty(deploymentName))
        {
            Logger.LogWarning("Unable to chat. Unable to find a deployment id '{DeploymentId}' or the default deployment", context.DeploymentId);

            yield break;
        }

        var chatClient = await BuildClientAsync(connectionName, context, deploymentName);

        var chatOptions = await GetChatOptionsAsync(context, deploymentName, true);

        var prompts = GetPrompts(messages, context);

        await foreach (var update in chatClient.GetStreamingResponseAsync(prompts, chatOptions, cancellationToken))
        {
            ProcessChatResponseUpdate(update, prompts);

            yield return update;
        }
    }

    private static List<ChatMessage> GetPrompts(IEnumerable<ChatMessage> messages, AICompletionContext context)
    {
        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(x.Text));

        var prompts = new List<ChatMessage>();

        var systemMessage = GetSystemMessage(context);

        if (!string.IsNullOrEmpty(systemMessage))
        {
            prompts.Add(new ChatMessage(ChatRole.System, systemMessage));
        }

        if (context.PastMessagesCount > 1)
        {
            var skip = GetTotalMessagesToSkip(chatMessages.Count(), context.PastMessagesCount.Value);

            prompts.AddRange(chatMessages.Skip(skip).Take(context.PastMessagesCount.Value));
        }
        else
        {
            prompts.AddRange(chatMessages);
        }

        return prompts;
    }

    private async Task<ChatOptions> GetChatOptionsAsync(AICompletionContext context, string deploymentName, bool isStreaming)
    {
        var chatOptions = new ChatOptions()
        {
            Temperature = context.Temperature,
            TopP = context.TopP,
            FrequencyPenalty = context.FrequencyPenalty,
            PresencePenalty = context.PresencePenalty,
            MaxOutputTokens = context.MaxTokens,
        };

        var supportFunctions = SupportFunctionInvocation(context, deploymentName);

        var configureContext = new CompletionServiceConfigureContext(chatOptions, context, supportFunctions)
        {
            DeploymentName = deploymentName,
            ProviderName = ProviderName,
            ImplemenationName = Name,
            IsStreaming = isStreaming,
        };

        await _handlers.InvokeAsync((handler, ctx) => handler.ConfigureAsync(ctx), configureContext, Logger);

        if (!supportFunctions || (chatOptions.Tools is not null && chatOptions.Tools.Count == 0))
        {
            chatOptions.Tools = null;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        ConfigureChatOptions(chatOptions, deploymentName);
#pragma warning restore CS0618 // Type or member is obsolete

        await ConfigureChatOptionsAsync(configureContext);

        return chatOptions;
    }

    private async ValueTask<IChatClient> BuildClientAsync(string connectionName, AICompletionContext context, string modelName)
    {
        var client = await _aIClientFactory.CreateChatClientAsync(ProviderName, connectionName, modelName);

        var builder = new ChatClientBuilder(client);

        builder.UseLogging(LoggerFactory, ConfigureLogger);

        if (SupportFunctionInvocation(context, modelName))
        {
            builder.UseFunctionInvocation(LoggerFactory, ConfigureFunctionInvocation);
        }

        if (_defaultOptions.EnableDistributedCaching && context.UseCaching)
        {
            builder.UseDistributedCache(_distributedCache);
        }

        if (_defaultOptions.EnableOpenTelemetry)
        {
            builder.UseOpenTelemetry(LoggerFactory, sourceName: DefaultLogCategory, ConfigureOpenTelemetry);
        }

        return builder.Build();
    }
}

using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class NamedAICompletionClient : AICompletionServiceBase, IAICompletionClient
{
    private static readonly AIProfileMetadata _defaultMetadata = new();

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

    protected virtual void ConfigureChatOptions(ChatOptions options, string modelName)
    {
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

    public async Task<ChatResponse> CompleteAsync(IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        if (!ProviderOptions.Providers.TryGetValue(ProviderName, out var provider))
        {
            throw new ArgumentException($"Provider '{ProviderName}' not found.");
        }

        var connectionName = GetDefaultConnectionName(provider, context.Profile);

        if (string.IsNullOrEmpty(connectionName))
        {
            Logger.LogWarning("Unable to chat. Unable to find the connection name associated with the profile with id '{ProfileId}' or a default DefaultConnectionName.", context.Profile?.Id);

            return null;
        }

        var deploymentName = GetDefaultDeploymentName(provider, connectionName);

        if (string.IsNullOrEmpty(deploymentName))
        {
            Logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile?.Id);

            return null;
        }

        if (context.Profile is null || !context.Profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            metadata = _defaultMetadata;
        }

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(x.Text));

        var skip = GetTotalMessagesToSkip(chatMessages.Count(), pastMessageCount);

        var prompts = new List<ChatMessage>()
        {
            new(ChatRole.System, GetSystemMessage(context, metadata))
        }.Concat(chatMessages.Skip(skip).Take(pastMessageCount))
        .ToList();

        try
        {
            var chatClient = await BuildClientAsync(connectionName, context, metadata, deploymentName);

            var chatOptions = await GetChatOptionsAsync(context, metadata, deploymentName);

            return await chatClient.GetResponseAsync(prompts, chatOptions, cancellationToken);
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

        var connectionName = GetDefaultConnectionName(provider, context.Profile);

        if (string.IsNullOrEmpty(connectionName))
        {
            Logger.LogWarning("Unable to chat. Unable to find the connection name associated with the profile with id '{ProfileId}' or a default DefaultConnectionName.", context.Profile?.Id);

            yield break;
        }

        var deploymentName = GetDefaultDeploymentName(provider, connectionName);

        if (string.IsNullOrEmpty(deploymentName))
        {
            Logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile?.Id);

            yield break;
        }

        if (context.Profile is null || !context.Profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            metadata = _defaultMetadata;
        }

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(x.Text));

        var skip = GetTotalMessagesToSkip(chatMessages.Count(), pastMessageCount);

        var prompts = new List<ChatMessage>()
        {
            new(ChatRole.System, GetSystemMessage(context, metadata))
        }.Concat(chatMessages.Skip(skip).Take(pastMessageCount))
        .ToList();

        var chatClient = await BuildClientAsync(connectionName, context, metadata, deploymentName);

        var chatOptions = await GetChatOptionsAsync(context, metadata, deploymentName);

        await foreach (var update in chatClient.GetStreamingResponseAsync(prompts, chatOptions, cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<ChatOptions> GetChatOptionsAsync(AICompletionContext context, AIProfileMetadata metadata, string deploymentName)
    {
        var chatOptions = new ChatOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokens = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        var supportFunctions = SupportFunctionInvocation(context, deploymentName);

        var configureContext = new CompletionServiceConfigureContext(chatOptions, context.Profile, supportFunctions);

        await _handlers.InvokeAsync((handler, ctx) => handler.ConfigureAsync(ctx), configureContext, Logger);

        if (!supportFunctions || (chatOptions.Tools is not null && chatOptions.Tools.Count == 0))
        {
            chatOptions.Tools = null;
        }

        ConfigureChatOptions(chatOptions, deploymentName);

        return chatOptions;
    }

    private async ValueTask<IChatClient> BuildClientAsync(string connectionName, AICompletionContext context, AIProfileMetadata metadata, string modelName)
    {
        var client = await _aIClientFactory.CreateChatClientAsync(ProviderName, connectionName, modelName);

        var builder = new ChatClientBuilder(client);

        builder.UseLogging(LoggerFactory, ConfigureLogger);

        if (SupportFunctionInvocation(context, modelName))
        {
            builder.UseFunctionInvocation(LoggerFactory, ConfigureFunctionInvocation);
        }

        if (_defaultOptions.EnableDistributedCaching && context.UseCaching && metadata.UseCaching)
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

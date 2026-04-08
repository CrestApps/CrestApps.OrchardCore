using System.Diagnostics;
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Services;

internal sealed class AICompletionUsageTrackingChatClient : DelegatingChatClient
{
    private readonly string _providerName;
    private readonly string _clientName;
    private readonly string _connectionName;
    private readonly string _deploymentName;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public AICompletionUsageTrackingChatClient(
        IChatClient innerClient,
        string providerName,
        string clientName,
        string connectionName,
        string deploymentName,
        IServiceProvider serviceProvider,
        ILogger logger)
        : base(innerClient)
    {
        _providerName = providerName;
        _clientName = clientName;
        _connectionName = connectionName;
        _deploymentName = deploymentName;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        stopwatch.Stop();

        await RecordUsageAsync(response, options, stopwatch.Elapsed.TotalMilliseconds, false, cancellationToken);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }

        stopwatch.Stop();

        if (updates.Count > 0)
        {
            await RecordUsageAsync(updates.ToChatResponse(), options, stopwatch.Elapsed.TotalMilliseconds, true, cancellationToken);
        }
    }

    private async Task RecordUsageAsync(
        ChatResponse response,
        ChatOptions options,
        double responseLatencyMs,
        bool isStreaming,
        CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return;
        }

        if (!_serviceProvider.GetRequiredService<IOptions<GeneralAIOptions>>().Value.EnableAIUsageTracking)
        {
            return;
        }

        var observers = _serviceProvider.GetServices<IAICompletionUsageObserver>();

        if (!observers.Any())
        {
            return;
        }

        var completionContext = ResolveCompletionContext(options);
        var clientName = ResolveClientName(options);
        var additionalProperties = ResolveAdditionalProperties(options, completionContext);

        var record = AICompletionUsageRecordFactory.Create(
            additionalProperties,
            _providerName,
            clientName,
            _connectionName,
            _deploymentName,
            response.ModelId,
            response.ResponseId,
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0,
            response.Usage?.TotalTokenCount ?? 0,
            responseLatencyMs,
            isStreaming);

        await observers.InvokeHandlersAsync((observer, usageRecord) => observer.UsageRecordedAsync(usageRecord, cancellationToken), record, _logger);
    }

    private static AICompletionContext ResolveCompletionContext(ChatOptions options)
    {
        if (options?.AdditionalProperties?.TryGetValue(AICompletionContextKeys.CompletionContext, out var completionContextValue) == true &&
            completionContextValue is AICompletionContext completionContext)
        {
            return completionContext;
        }

        return AIInvocationScope.Current?.CompletionContext;
    }

    private string ResolveClientName(ChatOptions options)
    {
        if (options?.AdditionalProperties?.TryGetValue(AICompletionContextKeys.ClientName, out var clientNameValue) == true &&
            clientNameValue is string clientName &&
            !string.IsNullOrEmpty(clientName))
        {
            return clientName;
        }

        return _clientName;
    }

    private static Dictionary<string, object> ResolveAdditionalProperties(
        ChatOptions options,
        AICompletionContext completionContext)
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (AIInvocationScope.Current?.CompletionContext?.AdditionalProperties is { Count: > 0 } scopedCompletionProperties)
        {
            CopyProperties(properties, scopedCompletionProperties);
        }

        if (completionContext?.AdditionalProperties is { Count: > 0 } completionProperties)
        {
            CopyProperties(properties, completionProperties);
        }

        if (options?.AdditionalProperties is { Count: > 0 } optionProperties)
        {
            CopyProperties(properties, optionProperties);
        }

        if (AIInvocationScope.Current?.ChatSession is { } session)
        {
            properties[AICompletionContextKeys.Session] = session;
        }

        if (AIInvocationScope.Current?.ChatInteraction is { } interaction)
        {
            properties[AICompletionContextKeys.Interaction] = interaction;
            properties[AICompletionContextKeys.InteractionId] = interaction.ItemId;
        }

        return properties;
    }

    private static void CopyProperties(
        Dictionary<string, object> destination,
        IReadOnlyDictionary<string, object> source)
    {
        foreach (var (key, value) in source)
        {
            destination[key] = value;
        }
    }
}

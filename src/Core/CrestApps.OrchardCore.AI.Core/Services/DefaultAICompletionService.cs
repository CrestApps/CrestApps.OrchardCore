using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AI.Exceptions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAICompletionService : IAICompletionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAICompletionHandler> _completionHandlers;
    private readonly ILogger _logger;

    public DefaultAICompletionService(
        IServiceProvider serviceProvider,
        IEnumerable<IAICompletionHandler> completionHandlers,
        ILogger<DefaultAICompletionService> logger)
    {
        _serviceProvider = serviceProvider;
        _completionHandlers = completionHandlers;
        _logger = logger;
    }

    public async Task<ChatCompletion> CompleteAsync(string clientName, IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        var client = _serviceProvider.GetKeyedService<IAICompletionClient>(clientName)
            ?? throw new UnregisteredCompletionClientException(clientName);

        var response = await client.CompleteAsync(messages, context, cancellationToken);

        var updateContext = new ReceivedMessageContext(response);

        await _completionHandlers.InvokeAsync((handler, ctx) => handler.ReceivedMessageAsync(ctx), updateContext, _logger);

        return response;
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(string clientName, IEnumerable<ChatMessage> messages, AICompletionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        var client = _serviceProvider.GetKeyedService<IAICompletionClient>(clientName)
            ?? throw new UnregisteredCompletionClientException(clientName);

        await foreach (var chunk in client.CompleteStreamingAsync(messages, context, cancellationToken))
        {
            var updateContext = new ReceivedUpdateContext(chunk);

            await _completionHandlers.InvokeAsync((handler, ctx) => handler.ReceivedUpdateAsync(ctx), updateContext, _logger);

            yield return chunk;
        }
    }
}

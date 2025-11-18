using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AI.Exceptions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAICompletionService : IAICompletionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAICompletionHandler> _completionHandlers;
    private readonly AIOptions _aiOptions;
    private readonly ILogger _logger;

    public DefaultAICompletionService(
        IServiceProvider serviceProvider,
        IEnumerable<IAICompletionHandler> completionHandlers,
        IOptions<AIOptions> aiOptions,
        ILogger<DefaultAICompletionService> logger)
    {
        _serviceProvider = serviceProvider;
        _completionHandlers = completionHandlers;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task<ChatResponse> CompleteAsync(string clientName, IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        if (!_aiOptions.Clients.TryGetValue(clientName, out var clientType))
        {
            throw new UnregisteredCompletionClientException(clientName);
        }

        var client = _serviceProvider.GetService(clientType) as IAICompletionClient;

        var response = await client.CompleteAsync(messages, context, cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException("Unable to generate a response. Ensure that the connection, and the deployment names are correct.")
        }

        var updateContext = new ReceivedMessageContext(response);

        await _completionHandlers.InvokeAsync((handler, ctx) => handler.ReceivedMessageAsync(ctx), updateContext, _logger);

        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(string clientName, IEnumerable<ChatMessage> messages, AICompletionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        if (!_aiOptions.Clients.TryGetValue(clientName, out var clientType))
        {
            throw new UnregisteredCompletionClientException(clientName);
        }

        var client = _serviceProvider.GetService(clientType) as IAICompletionClient;

        await foreach (var chunk in client.CompleteStreamingAsync(messages, context, cancellationToken))
        {
            var updateContext = new ReceivedUpdateContext(chunk);

            await _completionHandlers.InvokeAsync((handler, ctx) => handler.ReceivedUpdateAsync(ctx), updateContext, _logger);

            yield return chunk;
        }
    }
}

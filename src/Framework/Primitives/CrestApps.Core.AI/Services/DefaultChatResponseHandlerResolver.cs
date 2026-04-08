using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Resolves <see cref="IChatResponseHandler"/> instances by name from the DI container.
/// When the requested name is <see langword="null"/> or empty, returns the default AI handler.
/// When <see cref="ChatMode.Conversation"/> is active, always returns the AI handler.
/// </summary>
public sealed class DefaultChatResponseHandlerResolver : IChatResponseHandlerResolver
{
    private readonly IEnumerable<IChatResponseHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultChatResponseHandlerResolver(
        IEnumerable<IChatResponseHandler> handlers,
        ILogger<DefaultChatResponseHandlerResolver> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public IChatResponseHandler Resolve(string handlerName = null, ChatMode chatMode = ChatMode.TextInput)
    {
        // Conversation mode requires the AI orchestration pipeline for
        // speech-to-text and text-to-speech integration. Custom handlers
        // are not supported in this mode.
        if (chatMode == ChatMode.Conversation)
        {
            if (!string.IsNullOrWhiteSpace(handlerName)
                && !string.Equals(handlerName, ChatResponseHandlerNames.AI, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Chat response handler '{HandlerName}' was requested but conversation mode requires the AI handler. Falling back to AI.",
                    handlerName);
            }

            return ResolveAIHandler();
        }

        // When no name is specified, return the default AI handler.
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            return ResolveAIHandler();
        }

        foreach (var handler in _handlers)
        {
            if (string.Equals(handler.Name, handlerName, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        _logger.LogWarning(
            "Chat response handler '{HandlerName}' is not registered. Falling back to the default AI handler.",
            handlerName);

        return ResolveAIHandler();
    }

    public IEnumerable<IChatResponseHandler> GetAll()
        => _handlers;

    private IChatResponseHandler ResolveAIHandler()
    {
        foreach (var handler in _handlers)
        {
            if (string.Equals(handler.Name, ChatResponseHandlerNames.AI, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        throw new InvalidOperationException(
            "The default AI chat response handler is not registered. " +
            "Ensure CrestApps AI services are properly configured.");
    }
}

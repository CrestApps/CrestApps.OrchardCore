using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Resolves <see cref="IChatResponseHandler"/> instances by name from the DI container.
/// When the requested name is <see langword="null"/> or empty, returns the default AI handler.
/// </summary>
internal sealed class DefaultChatResponseHandlerResolver : IChatResponseHandlerResolver
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

    public IChatResponseHandler Resolve(string handlerName = null)
    {
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
            if (string.Equals(handler.Name, AIChatResponseHandler.HandlerName, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        throw new InvalidOperationException(
            "The default AI chat response handler is not registered. " +
            "Ensure CrestApps.OrchardCore.AI services are properly configured.");
    }
}

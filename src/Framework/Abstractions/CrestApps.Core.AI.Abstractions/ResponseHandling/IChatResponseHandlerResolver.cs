using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.ResponseHandling;

/// <summary>
/// Resolves the appropriate <see cref="IChatResponseHandler"/> for a chat session based
/// on the configured handler name.
/// </summary>
/// <remarks>
/// Resolution order: explicit name → default AI handler.
/// When <paramref name="handlerName"/> is <see langword="null"/> or empty, the built-in AI
/// handler is returned. When a name is specified but no matching handler is found,
/// implementations should fall back to the default AI handler.
/// When <paramref name="chatMode"/> is <see cref="ChatMode.Conversation"/>,
/// the AI handler is always returned regardless of the requested name because
/// conversation mode requires the AI orchestration pipeline for speech-to-text
/// and text-to-speech integration.
/// </remarks>
public interface IChatResponseHandlerResolver
{
    /// <summary>
    /// Resolves a chat response handler by name.
    /// </summary>
    /// <param name="handlerName">
    /// The handler name, or <see langword="null"/> / empty for the default AI handler.
    /// </param>
    /// <param name="chatMode">
    /// The active chat mode. When set to <see cref="ChatMode.Conversation"/>, the
    /// built-in AI handler is always returned regardless of <paramref name="handlerName"/>.
    /// </param>
    /// <returns>The resolved <see cref="IChatResponseHandler"/> instance.</returns>
    IChatResponseHandler Resolve(string handlerName = null, ChatMode chatMode = ChatMode.TextInput);

    /// <summary>
    /// Gets all registered <see cref="IChatResponseHandler"/> instances.
    /// </summary>
    /// <returns>An enumerable of all registered handlers.</returns>
    IEnumerable<IChatResponseHandler> GetAll();
}

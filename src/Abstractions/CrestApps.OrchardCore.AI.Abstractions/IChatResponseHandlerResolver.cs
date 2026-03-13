namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Resolves the appropriate <see cref="IChatResponseHandler"/> for a chat session based
/// on the configured handler name.
/// </summary>
/// <remarks>
/// Resolution order: explicit name → default AI handler.
/// When <paramref name="handlerName"/> is <see langword="null"/> or empty, the built-in AI
/// handler is returned. When a name is specified but no matching handler is found,
/// implementations should throw <see cref="InvalidOperationException"/>.
/// </remarks>
public interface IChatResponseHandlerResolver
{
    /// <summary>
    /// Resolves a chat response handler by name.
    /// </summary>
    /// <param name="handlerName">
    /// The handler name, or <see langword="null"/> / empty for the default AI handler.
    /// </param>
    /// <returns>The resolved <see cref="IChatResponseHandler"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="handlerName"/> is specified but no handler with that name is registered.
    /// </exception>
    IChatResponseHandler Resolve(string handlerName = null);

    /// <summary>
    /// Gets all registered <see cref="IChatResponseHandler"/> instances.
    /// </summary>
    /// <returns>An enumerable of all registered handlers.</returns>
    IEnumerable<IChatResponseHandler> GetAll();
}

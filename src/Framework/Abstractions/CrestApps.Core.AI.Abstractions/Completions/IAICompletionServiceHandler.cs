using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Completions;

/// <summary>
/// Handles per-request configuration of AI completion options, allowing dynamic
/// customization of model parameters, tool selections, and other settings
/// before a completion request is sent to the AI provider.
/// </summary>
public interface IAICompletionServiceHandler
{
    /// <summary>
    /// Called on every request to configure the <see cref="ChatOptions"/> in the <see cref="CompletionServiceConfigureContext"/>.
    /// This allows dynamic customization of the completion behavior depending on the request context.
    /// </summary>
    /// <param name="context">
    /// The <see cref="CompletionServiceConfigureContext"/> that provides access to request-specific options and settings.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ConfigureAsync(CompletionServiceConfigureContext context);
}

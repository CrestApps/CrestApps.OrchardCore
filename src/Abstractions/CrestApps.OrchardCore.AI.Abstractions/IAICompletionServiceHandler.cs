using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

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

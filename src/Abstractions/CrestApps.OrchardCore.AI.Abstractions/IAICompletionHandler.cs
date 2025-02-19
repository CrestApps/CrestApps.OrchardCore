using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAICompletionHandler
{
    /// <summary>
    /// Handles a received message asynchronously.
    /// </summary>
    /// <param name="context">The context containing details of the received message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceivedMessageAsync(ReceivedMessageContext context);

    /// <summary>
    /// Handles a received update asynchronously.
    /// </summary>
    /// <param name="context">The context containing details of the received update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceivedUpdateAsync(ReceivedUpdateContext context);
}

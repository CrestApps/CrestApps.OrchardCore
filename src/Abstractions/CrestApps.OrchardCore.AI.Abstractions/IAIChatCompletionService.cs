using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public interface IAIChatCompletionService
{
    /// <summary>
    /// Gets the unique technical name of the chat completion service implementation.
    /// This name is used to distinguish between different implementations of the service.
    /// Each implementation should return a distinct name that identifies it clearly.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends a series of messages to the AI chat service and returns the completion response.
    /// This method allows communication with the AI chat API by providing input messages and context.
    /// </summary>
    /// <param name="messages">A collection of messages that are part of the chat conversation.</param>
    /// <param name="context">The context that may provide additional parameters or configurations for the chat request.</param>
    /// <returns>A task representing the asynchronous operation, with the completion response as the result.</returns>
    Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default);
}

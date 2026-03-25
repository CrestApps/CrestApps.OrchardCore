using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public interface IAICompletionService
{
    /// <summary>
    /// Sends a series of messages to the AI chat service and returns the completion response.
    /// </summary>
    /// <param name="deployment">The deployment that identifies which AI client and model to use.</param>
    /// <param name="messages">A collection of messages that are part of the chat conversation.</param>
    /// <param name="context">The context that may provide additional parameters or configurations for the chat request.</param>
    /// <returns>A task representing the asynchronous operation, with the completion response as the result.</returns>
    Task<ChatResponse> CompleteAsync(AIDeployment deployment, IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams chat completion updates from the AI service in real time.
    /// </summary>
    /// <param name="deployment">The deployment that identifies which AI client and model to use.</param>
    /// <param name="messages">A list of chat messages that define the conversation history.</param>
    /// <param name="context">Additional context or parameters for configuring the AI request.</param>
    /// <param name="cancellationToken">A token to cancel the streaming operation if needed.</param>
    /// <returns>An asynchronous stream of chat completion updates, allowing real-time processing of AI responses.</returns>
    IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(AIDeployment deployment, IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default);
}

using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIChatCompletionService
{
    /// <summary>
    /// Gets the unique technical name of the chat completion service implementation.
    /// This name is used to distinguish between different implementations of the service.
    /// Each implementation should return a distinct name that identifies it clearly.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends a series of messages to the OpenAI chat service and returns the completion response.
    /// This method allows communication with the OpenAI chat API by providing input messages and context.
    /// </summary>
    /// <param name="messages">A collection of messages that are part of the chat conversation.</param>
    /// <param name="context">The context that may provide additional parameters or configurations for the chat request.</param>
    /// <returns>A task representing the asynchronous operation, with the completion response as the result.</returns>
    Task<OpenAIChatCompletionResponse> ChatAsync(IEnumerable<OpenAIChatCompletionMessage> messages, OpenAIChatCompletionContext context);
}

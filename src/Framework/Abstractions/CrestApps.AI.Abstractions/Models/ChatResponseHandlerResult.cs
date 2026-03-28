using Microsoft.Extensions.AI;

namespace CrestApps.AI.Models;

/// <summary>
/// Represents the result of an <see cref="IChatResponseHandler"/> processing a chat prompt.
/// A result is either <em>streaming</em> (the response is available immediately as a stream
/// of <see cref="ChatResponseUpdate"/> chunks) or <em>deferred</em> (the response will be
/// delivered asynchronously at a later time, e.g., via webhook).
/// </summary>
public sealed class ChatResponseHandlerResult
{
    /// <summary>
    /// Gets a value indicating whether the response is deferred.
    /// When <see langword="true"/>, the hub should save the user prompt and complete
    /// without waiting for an assistant response. The response will be delivered
    /// asynchronously (e.g., via webhook, background task, or external callback).
    /// </summary>
    public bool IsDeferred { get; init; }

    /// <summary>
    /// Gets the streaming response. Only available when <see cref="IsDeferred"/> is <see langword="false"/>.
    /// Each <see cref="ChatResponseUpdate"/> contains a partial text chunk to be streamed to the client.
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> ResponseStream { get; init; }

    /// <summary>
    /// Creates a deferred result, indicating the response will arrive later.
    /// </summary>
    /// <returns>A new <see cref="ChatResponseHandlerResult"/> with <see cref="IsDeferred"/> set to <see langword="true"/>.</returns>
    public static ChatResponseHandlerResult Deferred()
        => new() { IsDeferred = true };

    /// <summary>
    /// Creates a streaming result with the given response stream.
    /// </summary>
    /// <param name="stream">The asynchronous stream of response updates.</param>
    /// <returns>A new <see cref="ChatResponseHandlerResult"/> with the given <paramref name="stream"/>.</returns>
    public static ChatResponseHandlerResult Streaming(IAsyncEnumerable<ChatResponseUpdate> stream)
        => new() { ResponseStream = stream };
}

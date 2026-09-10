using System.Net.WebSockets;

namespace CrestApps.OrchardCore.WebSockets;

/// <summary>
/// Coordinates the hand-off of a single WebSocket between the two halves of a provider-initiated flow that never
/// share a call stack: the request that started the flow (and is awaiting the socket), and the endpoint that
/// accepts the socket the provider dials back. The starter awaits <see cref="ConnectedTask"/>; the endpoint
/// completes it with the accepted socket, then parks on <see cref="ReleasedTask"/> so the request (and therefore
/// the socket) stays alive until the consumer is done with it.
/// </summary>
public sealed class WebSocketRendezvous
{
    private readonly TaskCompletionSource<WebSocket> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets the task that completes with the accepted WebSocket once the provider dials the hosting endpoint.
    /// </summary>
    public Task<WebSocket> ConnectedTask => _connected.Task;

    /// <summary>
    /// Gets the task the endpoint awaits after handing over the socket. It completes when the consumer releases the
    /// rendezvous, letting the endpoint return so the host can tear the request down.
    /// </summary>
    public Task ReleasedTask => _released.Task;

    /// <summary>
    /// Completes <see cref="ConnectedTask"/> with the accepted socket. Called by the endpoint.
    /// </summary>
    /// <param name="webSocket">The accepted socket.</param>
    /// <returns><see langword="true"/> when this rendezvous accepted the socket; otherwise, <see langword="false"/>.</returns>
    public bool TryComplete(WebSocket webSocket)
        => _connected.TrySetResult(webSocket);

    /// <summary>
    /// Fails <see cref="ConnectedTask"/> so a still-waiting starter stops awaiting. Called when the starter gives up
    /// (a timeout, or a failure after registration).
    /// </summary>
    public void Abort()
        => _connected.TrySetException(new OperationCanceledException("The WebSocket rendezvous was aborted before the provider connected."));

    /// <summary>
    /// Signals that the consumer is done with the socket, releasing the endpoint parked on <see cref="ReleasedTask"/>.
    /// </summary>
    public void Release()
        => _released.TrySetResult();
}

using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Defines the contract for a persistent relay connection to an external system
/// (e.g., a third-party live-agent platform) for real-time bidirectional communication.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the webhook pattern where the external system calls back into your application,
/// an <see cref="IExternalChatRelay"/> maintains a persistent connection so that events
/// such as typing indicators, agent-connected notifications, wait-time updates,
/// and chat messages flow in real time without polling.
/// </para>
/// <para>
/// This interface is protocol-agnostic. Implementations can use any transport:
/// WebSocket, SSE (Server-Sent Events), gRPC streaming, WebRTC data channels,
/// message queues, event buses, or any other protocol. The relay infrastructure
/// manages connection lifecycles and event routing regardless of the underlying transport.
/// </para>
/// <para>
/// Implementations should handle reconnection logic and graceful shutdown. The relay is
/// managed by <see cref="IExternalChatRelayManager"/>, which tracks active relay instances
/// by session ID and disposes them when sessions end.
/// </para>
/// </remarks>
public interface IExternalChatRelay : IAsyncDisposable
{
    /// <summary>
    /// Determines whether the relay is currently connected to the external system.
    /// Implementations may perform a network request to verify the connection status.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the relay is connected; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Establishes the connection to the external system for the given session.
    /// </summary>
    /// <param name="context">The relay context containing session identity and services.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConnectAsync(ExternalChatRelayContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a user prompt to the external system via the relay connection.
    /// </summary>
    /// <param name="text">The user's message text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SendPromptAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a signal (e.g., thumbs up, thumbs down, user typing) to the external system.
    /// </summary>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="data">Optional key-value data associated with the signal.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SendSignalAsync(string signalName, IDictionary<string, string> data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully disconnects from the external system.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

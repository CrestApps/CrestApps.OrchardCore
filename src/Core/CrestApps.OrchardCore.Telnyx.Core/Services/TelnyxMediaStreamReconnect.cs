using CrestApps.OrchardCore.WebSockets;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// What a Telnyx media session needs to take a stream back after it broke: the token Telnyx dials back with, the
/// registry that hands the new socket over, and how to start the stream again when Telnyx gives up on it.
/// </summary>
internal sealed class TelnyxMediaStreamReconnect
{
    /// <summary>
    /// How long a broken stream is waited for by default: long enough for Telnyx to dial back in or for a restarted
    /// stream to connect, short enough that a caller left in silence is not left there for long.
    /// </summary>
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Gets the registry the stream's token is kept registered in while the session lives.
    /// </summary>
    public required IWebSocketConnectionRegistry Registry { get; init; }

    /// <summary>
    /// Gets the correlation token in the stream URL Telnyx dials.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Gets the command that starts the stream again, to the same URL, after Telnyx reported it failed.
    /// </summary>
    public required Func<CancellationToken, Task> RestartStreaming { get; init; }

    /// <summary>
    /// Gets the tracker that delivers Telnyx's <c>streaming.failed</c> report to the session.
    /// </summary>
    public TelnyxMediaStreamTracker Tracker { get; init; }

    /// <summary>
    /// Gets the logger the session reports reconnects and losses to.
    /// </summary>
    public ILogger Logger { get; init; }

    /// <summary>
    /// Gets how long a broken stream is waited for before the call is handed on.
    /// </summary>
    public TimeSpan Grace { get; init; } = DefaultGrace;
}

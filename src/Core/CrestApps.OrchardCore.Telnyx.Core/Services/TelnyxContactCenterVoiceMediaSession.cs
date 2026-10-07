using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.WebSockets;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A bidirectional media session backed by a Telnyx media-streaming WebSocket. Caller audio arrives as <c>media</c>
/// events and is surfaced through <see cref="ReadIncomingAsync"/>; audio written through
/// <see cref="WriteOutgoingAsync"/> is sent back as a <c>media</c> event Telnyx plays into the call. Telnyx handles
/// all RTP framing, so both directions exchange raw mu-law payloads.
/// </summary>
/// <remarks>
/// <para>
/// A stream can break while the call stays up: something between Telnyx and this server drops the socket, and
/// Telnyx dials the same stream URL again. That callback used to be refused, because the correlation token was
/// claimed once and gone, so the call went on with nobody hearing anybody. Given a
/// <see cref="TelnyxMediaStreamReconnect"/>, the session keeps the token registered for as long as it lives, takes
/// the socket Telnyx dials back in place of the one that broke, and carries on reading and writing on it.
/// </para>
/// <para>
/// A broken socket does not always close on this side, so Telnyx's own report decides that the stream failed: on
/// <c>streaming.failed</c> the stream is started again, and when no socket arrives within the grace period the read
/// ends with <see cref="ContactCenterVoiceMediaLostException"/>, so the call is handed on rather than left silent.
/// </para>
/// </remarks>
internal sealed class TelnyxContactCenterVoiceMediaSession : IContactCenterVoiceMediaSession
{
    // Telnyx media frames are small (20 ms of 8 kHz mu-law is ~160 bytes). Cap a reassembled message so a hostile or
    // malfunctioning peer cannot grow the receive buffer without bound before the size guard closes the socket.
    private const int MaxMessageBytes = 64 * 1024;
    private const int ReceiveChunkBytes = 8 * 1024;

    // How often a reader waiting on a replacement socket looks again.
    private static readonly TimeSpan _replacementPollInterval = TimeSpan.FromMilliseconds(100);

    // A reconnect that arrived this recently already replaced the stream a later failure report is about.
    private static readonly TimeSpan _recentReplacementWindow = TimeSpan.FromSeconds(3);

    private readonly IContactCenterFeatureWorkLease _workLease;
    private readonly Func<CancellationToken, Task> _stop;
    private readonly TelnyxMediaStreamReconnect _reconnect;
    private readonly IDisposable _tracking;
    private readonly object _gate = new();

    // Replaced together when Telnyx dials the stream back in, so both are read under _gate or with Volatile.
    private WebSocket _webSocket;
    private WebSocketRendezvous _connection;
    private WebSocketRendezvous _standby;
    private int _generation;
    private long _lastReplacedTicks;
    private int _streamFailed;
    private int _lost;

    // These semaphores are intentionally never disposed, matching the Asterisk media session: their wait handle is
    // never accessed so no unmanaged handle is allocated, and disposing them during teardown would race a concurrent
    // StopAsync that still holds or is about to release the lock.
    private readonly SemaphoreSlim _stopLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _cleanupCompleted;
    private int _stopped;
    private int _leaseReleased;
    private int _disposed;

    public TelnyxContactCenterVoiceMediaSession(
        string sessionId,
        string providerCallId,
        WebSocket webSocket,
        IContactCenterFeatureWorkLease workLease,
        WebSocketRendezvous connection,
        Func<CancellationToken, Task> stop,
        TelnyxMediaStreamReconnect reconnect = null)
    {
        SessionId = sessionId;
        ProviderCallId = providerCallId;
        _webSocket = webSocket;
        _workLease = workLease;
        _connection = connection;
        _stop = stop;
        _reconnect = reconnect;

        if (reconnect is not null)
        {
            _tracking = reconnect.Tracker?.Track(providerCallId, OnStreamFailed);
            _ = ArmStandbyAsync();
        }
    }

    public string SessionId { get; }

    public string ProviderCallId { get; }

    public ContactCenterVoiceMediaFormat IncomingFormat { get; } = CreateFormat();

    public ContactCenterVoiceMediaFormat OutgoingFormat { get; } = CreateFormat();

    public async IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sequenceNumber = 0L;
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveChunkBytes);
        var messageStream = new MemoryStream();

        // A caller that stops reading stops the stream the orderly way. Handing the token to ReceiveAsync instead
        // would abort the socket the moment it was cancelled, because cancelling a pending WebSocket receive
        // aborts the connection: Telnyx then saw the stream vanish, reported streaming.failed with reason
        // "disconnected" and dialled back in, on every session that ended with the conversation.
        using var stopOnCancel = cancellationToken.Register(static state => ((TelnyxContactCenterVoiceMediaSession)state).StopInBackground(), this);

        try
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _stopped) == 0)
            {
                var generation = Volatile.Read(ref _generation);
                var webSocket = Volatile.Read(ref _webSocket);
                var ended = false;
                ValueWebSocketReceiveResult result = default;

                try
                {
                    result = await webSocket.ReceiveAsync(buffer.AsMemory(0, ReceiveChunkBytes), CancellationToken.None);
                    ended = result.MessageType == WebSocketMessageType.Close;
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or WebSocketException)
                {
                    ended = true;
                }

                if (ended)
                {
                    // The socket is gone, but the stream may not be: Telnyx dials a broken stream back in, and a
                    // socket it replaced is closed here on purpose. A half-read message belonged to the old one.
                    if (await AwaitReplacementAsync(generation, cancellationToken))
                    {
                        messageStream.SetLength(0);

                        continue;
                    }

                    break;
                }

                if (messageStream.Length + result.Count > MaxMessageBytes)
                {
                    // Oversized message: drop what has accumulated and resynchronize on the next message boundary
                    // rather than closing the whole session for one bad frame.
                    messageStream.SetLength(0);

                    if (result.EndOfMessage)
                    {
                        continue;
                    }

                    continue;
                }

                messageStream.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage)
                {
                    continue;
                }

                var kind = TelnyxMediaStreamMessages.ReadInbound(
                    messageStream.GetBuffer().AsSpan(0, (int)messageStream.Length),
                    out var payload);

                messageStream.SetLength(0);

                if (kind == TelnyxMediaStreamMessages.InboundKind.Stop)
                {
                    yield break;
                }

                if (kind == TelnyxMediaStreamMessages.InboundKind.Media)
                {
                    yield return new ContactCenterVoiceMediaFrame
                    {
                        SequenceNumber = sequenceNumber++,
                        Data = payload,
                    };
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await messageStream.DisposeAsync();
        }

        // Telnyx said the stream failed and it did not come back, with the call still up. That is not a caller
        // who hung up, and must not read like one: they are still holding the phone.
        if (Volatile.Read(ref _lost) != 0 && Volatile.Read(ref _stopped) == 0 && !cancellationToken.IsCancellationRequested)
        {
            throw new ContactCenterVoiceMediaLostException(
                $"The Telnyx media stream for call '{ProviderCallId}' failed and could not be restored.");
        }
    }

    public async ValueTask WriteOutgoingAsync(
        ContactCenterVoiceMediaFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (Volatile.Read(ref _stopped) != 0)
        {
            throw new InvalidOperationException("The Telnyx media session has already stopped.");
        }

        if (frame.Data.IsEmpty)
        {
            return;
        }

        var message = TelnyxMediaStreamMessages.CreateMediaMessage(frame.Data.Span);

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await Volatile.Read(ref _webSocket).SendAsync(
                message.AsMemory(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        catch (Exception exception) when (IsBrokenSocket(exception))
        {
            // The socket broke under the call. The audio is lost either way: Telnyx dials the stream back in and
            // the next frame goes to the new socket, or the read reports the stream lost. Throwing here instead
            // would end the call over a frame nobody could have heard.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Tells Telnyx to drop the audio it has queued for playback and not yet played.
    /// </summary>
    /// <remarks>
    /// Telnyx plays what it is sent in order and at the line's rate, so audio written faster than that waits on its
    /// side. A caller who talks over the assistant would otherwise hear the rest of what was queued. Sent under the
    /// same lock as the audio, because a WebSocket takes one send at a time and the clear arrives while speech and
    /// the room bed are both being written. A stopped session has nothing left to clear, so that is not an error.
    /// </remarks>
    public async ValueTask ClearOutgoingAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _stopped) != 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await Volatile.Read(ref _webSocket).SendAsync(
                TelnyxMediaStreamMessages.ClearMessage,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        catch (Exception exception) when (IsBrokenSocket(exception))
        {
            // Nothing queued on a socket that broke is going to play anyway.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _stopped, 1);

        await _stopLock.WaitAsync(CancellationToken.None);

        try
        {
            if (Volatile.Read(ref _cleanupCompleted) != 0)
            {
                return;
            }

            using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Telnyx is told to stop streaming before the socket is dropped. Dropping it first made every session end
            // in streaming.failed with reason "disconnected", on calls that had worked, so a real stream failure
            // could not be told apart from a normal ending. The abort afterwards still unblocks an in-flight
            // ReceiveAsync, so the read loop ends without waiting on the peer to close its side.
            // No more callbacks for this stream: the token stops being claimable before Telnyx is told to stop.
            await DisarmReconnectAsync();

            try
            {
                await _stop(cleanupCancellation.Token);
            }
            finally
            {
                var webSocket = Volatile.Read(ref _webSocket);

                webSocket.Abort();
                webSocket.Dispose();
            }

            Volatile.Write(ref _cleanupCompleted, 1);
        }
        finally
        {
            _stopLock.Release();

            if (Volatile.Read(ref _cleanupCompleted) != 0)
            {
                // Let the media-stream endpoint return so ASP.NET Core can tear the request down.
                Volatile.Read(ref _connection).Release();

                if (Interlocked.Exchange(ref _leaseReleased, 1) == 0)
                {
                    _workLease.Dispose();
                }
            }
        }
    }

    private void StopInBackground()
        => _ = StopQuietlyAsync();

    private async Task StopQuietlyAsync()
    {
        try
        {
            await StopAsync();
        }
        catch
        {
            // Stopping is best effort here; the owner stops the session again when it disposes it, and that call
            // is the one whose failure is observed.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await StopAsync();
        }
        finally
        {
            Volatile.Read(ref _connection).Release();

            if (Interlocked.Exchange(ref _leaseReleased, 1) == 0)
            {
                _workLease.Dispose();
            }
        }
    }

    /// <summary>
    /// Keeps the stream's token claimable, so the socket Telnyx dials back in after a break is handed to this session.
    /// </summary>
    private async Task ArmStandbyAsync()
    {
        if (_reconnect is null || Volatile.Read(ref _stopped) != 0)
        {
            return;
        }

        WebSocketRendezvous standby;

        try
        {
            standby = await _reconnect.Registry.RegisterAsync(_reconnect.Token, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The call goes on as it always did, only without a way back from a broken stream.
            _reconnect.Logger?.LogWarning(
                exception,
                "Could not keep the Telnyx media stream for call {CallControlId} open to a reconnect.",
                ProviderCallId.SanitizeLogValue());

            return;
        }

        var stopped = false;

        lock (_gate)
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                stopped = true;
            }
            else
            {
                _standby = standby;
            }
        }

        if (stopped)
        {
            await _reconnect.Registry.RemoveAsync(_reconnect.Token, CancellationToken.None);
            standby.Abort();

            return;
        }

        _ = standby.ConnectedTask.ContinueWith(
            task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    OnReconnected(task.Result, standby);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Carries on over the socket Telnyx dialed back in, and lets go of the one it replaced.
    /// </summary>
    private void OnReconnected(WebSocket webSocket, WebSocketRendezvous connection)
    {
        WebSocket replaced;
        WebSocketRendezvous replacedConnection;

        lock (_gate)
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                webSocket.Abort();
                webSocket.Dispose();
                connection.Release();

                return;
            }

            replaced = _webSocket;
            replacedConnection = _connection;
            _standby = null;

            Volatile.Write(ref _webSocket, webSocket);
            Volatile.Write(ref _connection, connection);
            Interlocked.Exchange(ref _lastReplacedTicks, DateTime.UtcNow.Ticks);
            Volatile.Write(ref _streamFailed, 0);
            Interlocked.Increment(ref _generation);
        }

        // Closing the replaced socket also wakes a read still waiting on it, and the read moves to the new one.
        replaced.Abort();
        replaced.Dispose();
        replacedConnection.Release();

        if (_reconnect.Logger?.IsEnabled(LogLevel.Information) == true)
        {
            _reconnect.Logger.LogInformation(
                "Telnyx reconnected the media stream for call {CallControlId}; the call carries on over the new connection.",
                ProviderCallId.SanitizeLogValue());
        }

        _ = ArmStandbyAsync();
    }

    /// <summary>
    /// Waits for Telnyx to dial a broken stream back in.
    /// </summary>
    /// <returns><see langword="true"/> when a new socket replaced the one that ended.</returns>
    private async Task<bool> AwaitReplacementAsync(int generation, CancellationToken cancellationToken)
    {
        if (_reconnect is null)
        {
            return false;
        }

        var deadline = DateTime.UtcNow + _reconnect.Grace;

        while (true)
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return true;
            }

            if (Volatile.Read(ref _lost) != 0 || Volatile.Read(ref _stopped) != 0 || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            // A socket that ends with no failure reported is most often the call ending. Once Telnyx has said the
            // stream failed, the recovery decides instead, so a restart under way is not given up on early.
            if (Volatile.Read(ref _streamFailed) == 0 && DateTime.UtcNow >= deadline)
            {
                return false;
            }

            try
            {
                await Task.Delay(_replacementPollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Called when Telnyx reports the stream failed: the stream is started again, and given up on if it does not
    /// come back in time.
    /// </summary>
    private void OnStreamFailed()
    {
        if (_reconnect is null || Volatile.Read(ref _stopped) != 0 || Volatile.Read(ref _lost) != 0)
        {
            return;
        }

        // Telnyx dialed back in on its own a moment ago; the failure it reports is the socket that was replaced.
        if (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastReplacedTicks) < _recentReplacementWindow.Ticks)
        {
            return;
        }

        if (Interlocked.Exchange(ref _streamFailed, 1) != 0)
        {
            return;
        }

        _ = RecoverAsync(Volatile.Read(ref _generation));
    }

    private async Task RecoverAsync(int generation)
    {
        _reconnect.Logger?.LogWarning(
            "Telnyx reported the media stream for call {CallControlId} failed; starting it again.",
            ProviderCallId.SanitizeLogValue());

        try
        {
            using var restartCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _reconnect.RestartStreaming(restartCancellation.Token);
        }
        catch (Exception exception)
        {
            _reconnect.Logger?.LogWarning(
                exception,
                "Telnyx did not accept restarting the media stream for call {CallControlId}.",
                ProviderCallId.SanitizeLogValue());
        }

        var deadline = DateTime.UtcNow + _reconnect.Grace;

        while (DateTime.UtcNow < deadline)
        {
            if (Volatile.Read(ref _generation) != generation || Volatile.Read(ref _stopped) != 0)
            {
                return;
            }

            await Task.Delay(_replacementPollInterval);
        }

        if (Volatile.Read(ref _generation) != generation || Volatile.Read(ref _stopped) != 0)
        {
            return;
        }

        Volatile.Write(ref _lost, 1);

        _reconnect.Logger?.LogError(
            "The media stream for call {CallControlId} failed and did not come back within {Seconds} seconds; the call is handed on.",
            ProviderCallId.SanitizeLogValue(),
            _reconnect.Grace.TotalSeconds);

        // A broken socket can sit open on this side forever; closing it is what lets the read report the loss.
        Volatile.Read(ref _webSocket).Abort();
    }

    private async Task DisarmReconnectAsync()
    {
        if (_reconnect is null)
        {
            return;
        }

        _tracking?.Dispose();

        WebSocketRendezvous standby;

        lock (_gate)
        {
            standby = _standby;
            _standby = null;
        }

        try
        {
            await _reconnect.Registry.RemoveAsync(_reconnect.Token, CancellationToken.None);
        }
        catch (Exception exception)
        {
            if (_reconnect.Logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _reconnect.Logger.LogDebug(exception, "Could not withdraw the media stream token for call {CallControlId}.", ProviderCallId.SanitizeLogValue());
            }
        }

        // A socket that races in after this is refused by the endpoint, which closes it.
        standby?.Abort();
    }

    private bool IsBrokenSocket(Exception exception)
        => Volatile.Read(ref _stopped) == 0 &&
            exception is WebSocketException or ObjectDisposedException;


    private static ContactCenterVoiceMediaFormat CreateFormat()
    {
        return new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
            Channels = 1,
            FrameDurationMilliseconds = 20,
        };
    }
}

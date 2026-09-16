using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.WebSockets;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A bidirectional media session backed by a Telnyx media-streaming WebSocket. Caller audio arrives as <c>media</c>
/// events and is surfaced through <see cref="ReadIncomingAsync"/>; audio written through
/// <see cref="WriteOutgoingAsync"/> is sent back as a <c>media</c> event Telnyx plays into the call. Telnyx handles
/// all RTP framing, so both directions exchange raw mu-law payloads.
/// </summary>
internal sealed class TelnyxContactCenterVoiceMediaSession : IContactCenterVoiceMediaSession
{
    // Telnyx media frames are small (20 ms of 8 kHz mu-law is ~160 bytes). Cap a reassembled message so a hostile or
    // malfunctioning peer cannot grow the receive buffer without bound before the size guard closes the socket.
    private const int MaxMessageBytes = 64 * 1024;
    private const int ReceiveChunkBytes = 8 * 1024;

    private readonly WebSocket _webSocket;
    private readonly IContactCenterFeatureWorkLease _workLease;
    private readonly WebSocketRendezvous _connection;
    private readonly Func<CancellationToken, Task> _stop;

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
        Func<CancellationToken, Task> stop)
    {
        SessionId = sessionId;
        ProviderCallId = providerCallId;
        _webSocket = webSocket;
        _workLease = workLease;
        _connection = connection;
        _stop = stop;
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

        try
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _stopped) == 0)
            {
                ValueWebSocketReceiveResult result;

                try
                {
                    result = await _webSocket.ReceiveAsync(buffer.AsMemory(0, ReceiveChunkBytes), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (ObjectDisposedException)
                {
                    yield break;
                }
                catch (WebSocketException)
                {
                    yield break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    yield break;
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
            await _webSocket.SendAsync(
                message.AsMemory(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _stopped, 1);

        // Abort unblocks any in-flight ReceiveAsync immediately so the read loop ends without waiting on the peer.
        _webSocket.Abort();

        await _stopLock.WaitAsync(CancellationToken.None);

        try
        {
            if (Volatile.Read(ref _cleanupCompleted) != 0)
            {
                return;
            }

            using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                await _stop(cleanupCancellation.Token);
            }
            finally
            {
                _webSocket.Dispose();
            }

            Volatile.Write(ref _cleanupCompleted, 1);
        }
        finally
        {
            _stopLock.Release();

            if (Volatile.Read(ref _cleanupCompleted) != 0)
            {
                // Let the media-stream endpoint return so ASP.NET Core can tear the request down.
                _connection.Release();

                if (Interlocked.Exchange(ref _leaseReleased, 1) == 0)
                {
                    _workLease.Dispose();
                }
            }
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
            _connection.Release();

            if (Interlocked.Exchange(ref _leaseReleased, 1) == 0)
            {
                _workLease.Dispose();
            }
        }
    }

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

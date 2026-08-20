using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A minimal in-memory <see cref="WebSocket"/> for exercising the Telnyx media-stream session. Inbound messages are
/// queued with <see cref="EnqueueText"/>/<see cref="EnqueueClose"/> and surfaced through the memory-based
/// <see cref="ReceiveAsync(Memory{byte}, CancellationToken)"/>; outbound text sends are recorded in
/// <see cref="SentTextMessages"/>. <see cref="Abort"/> unblocks a pending receive by faulting it, mirroring how the
/// session aborts the socket on stop.
/// </summary>
internal sealed class FakeWebSocket : WebSocket
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly CancellationTokenSource _abort = new();
    private byte[] _current;
    private int _currentOffset;
    private WebSocketState _state = WebSocketState.Open;

    public List<string> SentTextMessages { get; } = [];

    public bool Aborted { get; private set; }

    public bool Disposed { get; private set; }

    public void EnqueueText(string message)
        => _inbound.Writer.TryWrite(Encoding.UTF8.GetBytes(message));

    public void EnqueueClose()
        => _inbound.Writer.TryComplete();

    public override WebSocketState State => _state;

    public override WebSocketCloseStatus? CloseStatus { get; }

    public override string CloseStatusDescription { get; }

    public override string SubProtocol { get; }

    public override void Abort()
    {
        Aborted = true;
        _state = WebSocketState.Aborted;
        _abort.Cancel();
        _inbound.Writer.TryComplete();
    }

    public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abort.Token);

        if (_current is null)
        {
            try
            {
                if (!await _inbound.Reader.WaitToReadAsync(linked.Token) ||
                    !_inbound.Reader.TryRead(out _current))
                {
                    _state = WebSocketState.CloseReceived;

                    return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true);
                }
            }
            catch (OperationCanceledException) when (_abort.IsCancellationRequested)
            {
                throw new OperationCanceledException("The fake web socket was aborted.");
            }

            _currentOffset = 0;
        }

        var remaining = _current.Length - _currentOffset;
        var count = Math.Min(remaining, buffer.Length);
        _current.AsSpan(_currentOffset, count).CopyTo(buffer.Span);
        _currentOffset += count;

        var endOfMessage = _currentOffset >= _current.Length;

        if (endOfMessage)
        {
            _current = null;
        }

        return new ValueWebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage);
    }

    public override ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        SentTextMessages.Add(Encoding.UTF8.GetString(buffer.Span));

        return ValueTask.CompletedTask;
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;

        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Disposed = true;
        _abort.Cancel();
        _inbound.Writer.TryComplete();
    }
}

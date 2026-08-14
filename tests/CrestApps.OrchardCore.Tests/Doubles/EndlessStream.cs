namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A request body that never ends, standing in for a caller that omits its content length and keeps sending.
/// </summary>
public sealed class EndlessStream : Stream
{
    private const long ExhaustionCeilingBytes = 32L * 1024 * 1024;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public long BytesProduced { get; private set; }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (BytesProduced >= ExhaustionCeilingBytes)
        {
            return 0;
        }

        buffer.AsSpan(offset, count).Fill((byte)'a');
        BytesProduced += count;

        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (BytesProduced >= ExhaustionCeilingBytes)
        {
            return ValueTask.FromResult(0);
        }

        buffer.Span.Fill((byte)'a');
        BytesProduced += buffer.Length;

        return ValueTask.FromResult(buffer.Length);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

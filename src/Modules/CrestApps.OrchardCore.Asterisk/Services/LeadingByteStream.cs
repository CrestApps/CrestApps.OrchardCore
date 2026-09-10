namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// A read-only stream that replays a single already-read leading byte before delegating to an inner stream.
/// It is used to distinguish an empty chunked HTTP body (which reports no content length) from a non-empty one
/// by peeking one byte, without losing that byte from the recording that is then streamed to storage. The inner
/// stream is not owned by this wrapper; its owner remains responsible for disposing it.
/// </summary>
internal sealed class LeadingByteStream : Stream
{
    private readonly byte _leadingByte;
    private readonly Stream _inner;
    private bool _leadingByteConsumed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadingByteStream"/> class.
    /// </summary>
    /// <param name="leadingByte">The already-read byte to serve before the inner stream.</param>
    /// <param name="inner">The remaining stream, read after the leading byte. Ownership is retained by the caller.</param>
    public LeadingByteStream(byte leadingByte, Stream inner)
    {
        _leadingByte = leadingByte;
        _inner = inner;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        if (!_leadingByteConsumed)
        {
            _leadingByteConsumed = true;
            buffer[0] = _leadingByte;

            return 1;
        }

        return _inner.Read(buffer);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        if (!_leadingByteConsumed)
        {
            _leadingByteConsumed = true;
            buffer.Span[0] = _leadingByte;

            return 1;
        }

        return await _inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

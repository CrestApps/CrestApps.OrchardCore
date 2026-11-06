using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// A read-only stream that lazily decrypts a <see cref="RecordingMediaCryptoFormat"/> chunked container as it
/// is consumed. Reading pulls at most one authenticated frame from the source, verifies and decrypts it, and
/// serves the plaintext, so peak memory stays bounded to a small multiple of
/// <see cref="RecordingMediaCryptoFormat.ChunkSizeBytes"/> regardless of the recording size. Each frame's tag
/// and associated data are verified, so a tampered, reordered, or truncated container is rejected rather than
/// yielding altered or partial audio. The stream owns and disposes the underlying container source.
/// </summary>
/// <remarks>
/// <para>
/// When the source can seek, so can this stream. Every frame but the last holds exactly
/// <see cref="RecordingMediaCryptoFormat.ChunkSizeBytes"/> of plaintext, so frame <c>i</c> starts at a fixed
/// offset and the plaintext length follows from the container length. A read at any position seeks the source
/// to the frame that holds it and decrypts only that frame, which lets an HTTP range request be served without
/// decrypting the whole recording. Each frame is checked against the layout the container length implies (only
/// the last frame may be final, and it must have the implied length), and the final frame is authenticated as
/// soon as the stream opens, so a truncated or extended container is rejected and <see cref="Length"/> can be
/// trusted.
/// </para>
/// <para>
/// When the source cannot seek, the stream reads forward only and refuses to return a clean end-of-stream until
/// it has read the frame explicitly marked final, with nothing after it.
/// </para>
/// </remarks>
internal sealed class ChunkedAeadDecryptingReadStream : Stream
{
    private const int FullFrameSizeBytes = RecordingMediaCryptoFormat.FrameHeaderSizeBytes + RecordingMediaCryptoFormat.ChunkSizeBytes;

    private readonly Stream _ciphertext;
    private readonly AesGcm _aesGcm;
    private readonly byte[] _noncePrefix;
    private readonly byte[] _frameHeader;
    private readonly byte[] _cipherBuffer;
    private readonly byte[] _plaintextBuffer;
    private readonly byte[] _trailingProbe = new byte[1];

    // The seekable layout, derived from the container length.
    private readonly bool _canSeek;
    private readonly long _firstFrameOffset;
    private readonly long _finalFrameIndex;
    private readonly int _finalFrameLength;
    private readonly long _length;
    private long _position;
    private long _bufferedFrameIndex = -1;

    // The forward-only state, used when the source cannot seek.
    private int _plaintextOffset;
    private int _plaintextLength;
    private ulong _counter;
    private bool _finalFrameConsumed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedAeadDecryptingReadStream"/> class.
    /// </summary>
    /// <param name="ciphertext">
    /// The container stream positioned at the first frame. The stream takes ownership. When it can seek, its
    /// current position is taken as the offset of the first frame and its length as the end of the container.
    /// </param>
    /// <param name="aesGcm">The AES-GCM instance keyed with the unwrapped data key. The stream takes ownership.</param>
    /// <param name="noncePrefix">The recording's random nonce prefix read from the container header.</param>
    /// <exception cref="CryptographicException">The seekable container is too short to end with a frame.</exception>
    public ChunkedAeadDecryptingReadStream(Stream ciphertext, AesGcm aesGcm, byte[] noncePrefix)
    {
        _ciphertext = ciphertext;
        _aesGcm = aesGcm;
        _noncePrefix = noncePrefix;
        _frameHeader = new byte[RecordingMediaCryptoFormat.FrameHeaderSizeBytes];
        _cipherBuffer = new byte[RecordingMediaCryptoFormat.ChunkSizeBytes];
        _plaintextBuffer = new byte[RecordingMediaCryptoFormat.ChunkSizeBytes];
        _canSeek = ciphertext.CanSeek;

        if (!_canSeek)
        {
            return;
        }

        _firstFrameOffset = ciphertext.Position;

        var framesLength = ciphertext.Length - _firstFrameOffset;
        _finalFrameIndex = framesLength / FullFrameSizeBytes;

        var finalFrameSize = framesLength - (_finalFrameIndex * FullFrameSizeBytes);

        // The final frame holds less than a full chunk (possibly nothing), so it always leaves a remainder of at
        // least one frame header. Anything shorter means the container ends mid-frame or without its final frame.
        if (finalFrameSize < RecordingMediaCryptoFormat.FrameHeaderSizeBytes)
        {
            throw new CryptographicException("The recording media container is truncated: no final frame was found.");
        }

        _finalFrameLength = (int)(finalFrameSize - RecordingMediaCryptoFormat.FrameHeaderSizeBytes);
        _length = (_finalFrameIndex * RecordingMediaCryptoFormat.ChunkSizeBytes) + _finalFrameLength;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => _canSeek;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _canSeek ? _length : throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => _canSeek ? _position : throw new NotSupportedException();
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// Authenticates the final frame of a seekable container, proving the container has exactly the frames its
    /// length implies, so <see cref="Length"/> can be trusted before any range of the recording is read. The
    /// decrypted frame stays buffered, which also serves a player's first request for the end of the recording.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes once the final frame is verified.</returns>
    internal async ValueTask VerifyFinalFrameAsync(CancellationToken cancellationToken)
    {
        if (_canSeek)
        {
            await LoadFrameAsync(_finalFrameIndex, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_canSeek)
        {
            if (_position >= _length || buffer.IsEmpty)
            {
                return 0;
            }

            var frameIndex = _position / RecordingMediaCryptoFormat.ChunkSizeBytes;

            if (frameIndex != _bufferedFrameIndex)
            {
                LoadFrame(frameIndex);
            }

            return ServeAtPosition(buffer, frameIndex);
        }

        if (!EnsureFrame())
        {
            return 0;
        }

        return ServeFrom(buffer);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_canSeek)
        {
            if (_position >= _length || buffer.IsEmpty)
            {
                return 0;
            }

            var frameIndex = _position / RecordingMediaCryptoFormat.ChunkSizeBytes;

            if (frameIndex != _bufferedFrameIndex)
            {
                await LoadFrameAsync(frameIndex, cancellationToken);
            }

            return ServeAtPosition(buffer.Span, frameIndex);
        }

        if (!await EnsureFrameAsync(cancellationToken))
        {
            return 0;
        }

        return ServeFrom(buffer.Span);
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
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_canSeek)
        {
            throw new NotSupportedException();
        }

        var position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (position < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        }

        _position = position;

        return _position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int ServeAtPosition(Span<byte> buffer, long frameIndex)
    {
        // The position is before the end, so it falls inside the buffered frame's plaintext.
        var frameOffset = (int)(_position - (frameIndex * RecordingMediaCryptoFormat.ChunkSizeBytes));
        var toCopy = Math.Min(buffer.Length, _plaintextLength - frameOffset);
        _plaintextBuffer.AsSpan(frameOffset, toCopy).CopyTo(buffer);
        _position += toCopy;

        return toCopy;
    }

    private void LoadFrame(long frameIndex)
    {
        var plaintextLength = ExpectedFrameLength(frameIndex);
        SeekToFrame(frameIndex);

        try
        {
            _ciphertext.ReadExactly(_frameHeader);
            _ciphertext.ReadExactly(_cipherBuffer.AsSpan(0, plaintextLength));
        }
        catch (EndOfStreamException ex)
        {
            throw new CryptographicException("The recording media container is truncated: a frame is incomplete.", ex);
        }

        DecryptFrameAt(frameIndex, plaintextLength);
    }

    private async ValueTask LoadFrameAsync(long frameIndex, CancellationToken cancellationToken)
    {
        var plaintextLength = ExpectedFrameLength(frameIndex);
        SeekToFrame(frameIndex);

        try
        {
            await _ciphertext.ReadExactlyAsync(_frameHeader, cancellationToken);
            await _ciphertext.ReadExactlyAsync(_cipherBuffer.AsMemory(0, plaintextLength), cancellationToken);
        }
        catch (EndOfStreamException ex)
        {
            throw new CryptographicException("The recording media container is truncated: a frame is incomplete.", ex);
        }

        DecryptFrameAt(frameIndex, plaintextLength);
    }

    private int ExpectedFrameLength(long frameIndex)
    {
        return frameIndex == _finalFrameIndex ? _finalFrameLength : RecordingMediaCryptoFormat.ChunkSizeBytes;
    }

    private void SeekToFrame(long frameIndex)
    {
        var offset = _firstFrameOffset + (frameIndex * FullFrameSizeBytes);

        if (_ciphertext.Position != offset)
        {
            _ciphertext.Position = offset;
        }
    }

    private void DecryptFrameAt(long frameIndex, int expectedLength)
    {
        // Invalidate the buffer first, so a frame that fails verification is never served.
        _bufferedFrameIndex = -1;

        var flags = _frameHeader[0];
        var plaintextLength = BinaryPrimitives.ReadInt32LittleEndian(_frameHeader.AsSpan(1, 4));
        var isFinal = (flags & RecordingMediaCryptoFormat.FinalFrameFlag) != 0;

        // The container length fixes which frame is final and how long every frame is. A frame that disagrees
        // means the container was truncated or extended, even if the frame itself is authentic.
        if (isFinal != (frameIndex == _finalFrameIndex) || plaintextLength != expectedLength)
        {
            throw new CryptographicException("The recording media container does not match its length: it was truncated or extended.");
        }

        DecryptFrame((ulong)frameIndex, flags, plaintextLength);
        _bufferedFrameIndex = frameIndex;
    }

    private int ServeFrom(Span<byte> buffer)
    {
        var available = _plaintextLength - _plaintextOffset;
        var toCopy = Math.Min(buffer.Length, available);
        _plaintextBuffer.AsSpan(_plaintextOffset, toCopy).CopyTo(buffer);
        _plaintextOffset += toCopy;

        return toCopy;
    }

    private bool EnsureFrame()
    {
        if (_plaintextLength - _plaintextOffset > 0)
        {
            return true;
        }

        if (_finalFrameConsumed)
        {
            return false;
        }

        var headerRead = ReadUpTo(_frameHeader);

        if (headerRead == 0)
        {
            throw new CryptographicException("The recording media container is truncated: no final frame was found.");
        }

        var plaintextLength = ParseFrameHeader(headerRead, out var flags);

        try
        {
            _ciphertext.ReadExactly(_cipherBuffer.AsSpan(0, plaintextLength));
        }
        catch (EndOfStreamException ex)
        {
            throw new CryptographicException("The recording media container is truncated: a frame body is incomplete.", ex);
        }

        DecryptNextFrame(flags, plaintextLength);

        if (_finalFrameConsumed)
        {
            EnsureNoTrailingData();
        }

        return true;
    }

    private async ValueTask<bool> EnsureFrameAsync(CancellationToken cancellationToken)
    {
        if (_plaintextLength - _plaintextOffset > 0)
        {
            return true;
        }

        if (_finalFrameConsumed)
        {
            return false;
        }

        var headerRead = await ReadUpToAsync(_frameHeader, cancellationToken);

        if (headerRead == 0)
        {
            throw new CryptographicException("The recording media container is truncated: no final frame was found.");
        }

        var plaintextLength = ParseFrameHeader(headerRead, out var flags);

        try
        {
            await _ciphertext.ReadExactlyAsync(_cipherBuffer.AsMemory(0, plaintextLength), cancellationToken);
        }
        catch (EndOfStreamException ex)
        {
            throw new CryptographicException("The recording media container is truncated: a frame body is incomplete.", ex);
        }

        DecryptNextFrame(flags, plaintextLength);

        if (_finalFrameConsumed)
        {
            await EnsureNoTrailingDataAsync(cancellationToken);
        }

        return true;
    }

    private void EnsureNoTrailingData()
    {
        // The final frame is authenticated as final, but nothing else is; verify the source is physically
        // exhausted so appended or duplicated bytes after it are rejected rather than silently ignored. This
        // runs the instant the final frame is decrypted, before the terminating zero-length read is served, so
        // even a consumer that stops at the first end-of-stream still detects the trailing data.
        if (_ciphertext.ReadByte() != -1)
        {
            throw new CryptographicException("The recording media container has unexpected data after the final frame.");
        }
    }

    private async ValueTask EnsureNoTrailingDataAsync(CancellationToken cancellationToken)
    {
        var trailing = await _ciphertext.ReadAsync(_trailingProbe, cancellationToken);

        if (trailing != 0)
        {
            throw new CryptographicException("The recording media container has unexpected data after the final frame.");
        }
    }

    private int ParseFrameHeader(int headerRead, out byte flags)
    {
        if (headerRead < RecordingMediaCryptoFormat.FrameHeaderSizeBytes)
        {
            throw new CryptographicException("The recording media container is corrupt: an incomplete frame header was read.");
        }

        flags = _frameHeader[0];
        var plaintextLength = BinaryPrimitives.ReadInt32LittleEndian(_frameHeader.AsSpan(1, 4));

        if (plaintextLength is < 0 || plaintextLength > RecordingMediaCryptoFormat.ChunkSizeBytes)
        {
            throw new CryptographicException("The recording media container declares an invalid frame length.");
        }

        return plaintextLength;
    }

    private void DecryptNextFrame(byte flags, int plaintextLength)
    {
        DecryptFrame(_counter, flags, plaintextLength);

        _counter++;
        _plaintextOffset = 0;

        if ((flags & RecordingMediaCryptoFormat.FinalFrameFlag) != 0)
        {
            _finalFrameConsumed = true;
        }
    }

    private void DecryptFrame(ulong counter, byte flags, int plaintextLength)
    {
        Span<byte> nonce = stackalloc byte[RecordingMediaCryptoFormat.NonceSizeBytes];
        RecordingMediaCryptoFormat.BuildNonce(_noncePrefix, counter, nonce);

        Span<byte> associatedData = stackalloc byte[RecordingMediaCryptoFormat.AssociatedDataSizeBytes];
        RecordingMediaCryptoFormat.BuildAssociatedData(flags, plaintextLength, counter, associatedData);

        var tag = _frameHeader.AsSpan(5, RecordingMediaCryptoFormat.TagSizeBytes);

        _aesGcm.Decrypt(
            nonce,
            _cipherBuffer.AsSpan(0, plaintextLength),
            tag,
            _plaintextBuffer.AsSpan(0, plaintextLength),
            associatedData);

        _plaintextLength = plaintextLength;
    }

    private int ReadUpTo(Span<byte> buffer)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = _ciphertext.Read(buffer.Slice(total));

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private async ValueTask<int> ReadUpToAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await _ciphertext.ReadAsync(buffer.AsMemory(total), cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                _aesGcm.Dispose();
                _ciphertext.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _aesGcm.Dispose();
            await _ciphertext.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}

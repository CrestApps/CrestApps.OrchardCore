using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// A read-only stream that lazily decrypts a <see cref="RecordingMediaCryptoFormat"/> chunked container as it
/// is consumed. Reading pulls at most one authenticated frame from the source, verifies and decrypts it, and
/// serves the plaintext, so peak memory stays bounded to a small multiple of
/// <see cref="RecordingMediaCryptoFormat.ChunkSizeBytes"/> regardless of the recording size. Each frame's tag
/// and associated data are verified, and the stream refuses to return a clean end-of-stream until it has read
/// the frame explicitly marked final, so a tampered, reordered, or truncated container is rejected rather than
/// yielding altered or partial audio. The stream owns and disposes the underlying container source.
/// </summary>
internal sealed class ChunkedAeadDecryptingReadStream : Stream
{
    private readonly Stream _ciphertext;
    private readonly AesGcm _aesGcm;
    private readonly byte[] _noncePrefix;
    private readonly byte[] _frameHeader;
    private readonly byte[] _cipherBuffer;
    private readonly byte[] _plaintextBuffer;
    private readonly byte[] _trailingProbe = new byte[1];

    private int _plaintextOffset;
    private int _plaintextLength;
    private ulong _counter;
    private bool _finalFrameConsumed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedAeadDecryptingReadStream"/> class.
    /// </summary>
    /// <param name="ciphertext">The container stream positioned at the first frame. The stream takes ownership.</param>
    /// <param name="aesGcm">The AES-GCM instance keyed with the unwrapped data key. The stream takes ownership.</param>
    /// <param name="noncePrefix">The recording's random nonce prefix read from the container header.</param>
    public ChunkedAeadDecryptingReadStream(Stream ciphertext, AesGcm aesGcm, byte[] noncePrefix)
    {
        _ciphertext = ciphertext;
        _aesGcm = aesGcm;
        _noncePrefix = noncePrefix;
        _frameHeader = new byte[RecordingMediaCryptoFormat.FrameHeaderSizeBytes];
        _cipherBuffer = new byte[RecordingMediaCryptoFormat.ChunkSizeBytes];
        _plaintextBuffer = new byte[RecordingMediaCryptoFormat.ChunkSizeBytes];
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
        if (!EnsureFrame())
        {
            return 0;
        }

        return ServeFrom(buffer);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
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
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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

        DecryptFrame(flags, plaintextLength);

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

        DecryptFrame(flags, plaintextLength);

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

    private void DecryptFrame(byte flags, int plaintextLength)
    {
        Span<byte> nonce = stackalloc byte[RecordingMediaCryptoFormat.NonceSizeBytes];
        RecordingMediaCryptoFormat.BuildNonce(_noncePrefix, _counter, nonce);

        Span<byte> associatedData = stackalloc byte[RecordingMediaCryptoFormat.AssociatedDataSizeBytes];
        RecordingMediaCryptoFormat.BuildAssociatedData(flags, plaintextLength, _counter, associatedData);

        var tag = _frameHeader.AsSpan(5, RecordingMediaCryptoFormat.TagSizeBytes);

        _aesGcm.Decrypt(
            nonce,
            _cipherBuffer.AsSpan(0, plaintextLength),
            tag,
            _plaintextBuffer.AsSpan(0, plaintextLength),
            associatedData);

        _counter++;
        _plaintextOffset = 0;
        _plaintextLength = plaintextLength;

        if ((flags & RecordingMediaCryptoFormat.FinalFrameFlag) != 0)
        {
            _finalFrameConsumed = true;
        }
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

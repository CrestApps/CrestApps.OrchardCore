using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// A read-only stream that lazily encrypts a plaintext source into the
/// <see cref="RecordingMediaCryptoFormat"/> chunked container as it is consumed. Reading pulls at most one
/// plaintext chunk from the source, encrypts it into a single authenticated frame, and serves that frame, so
/// peak memory stays bounded to a small multiple of <see cref="RecordingMediaCryptoFormat.ChunkSizeBytes"/>
/// no matter how large the recording is. The container header (magic, version, wrapped data key, nonce prefix)
/// is emitted before the first frame. The plaintext source is never disposed by this stream; its owner keeps
/// responsibility for it.
/// </summary>
internal sealed class ChunkedAeadEncryptingReadStream : Stream
{
    private readonly Stream _plaintext;
    private readonly AesGcm _aesGcm;
    private readonly CancellationToken _cancellationToken;
    private readonly byte[] _noncePrefix;
    private readonly byte[] _header;
    private readonly byte[] _plaintextChunk;
    private readonly byte[] _frameBuffer;

    private byte[] _segment;
    private int _segmentOffset;
    private int _segmentLength;
    private ulong _counter;
    private bool _headerServed;
    private bool _finalFrameServed;
    private bool _segmentIsFinalFrame;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedAeadEncryptingReadStream"/> class.
    /// </summary>
    /// <param name="plaintext">The readable plaintext recording stream. Ownership is retained by the caller.</param>
    /// <param name="protector">The data protector used to wrap the per-recording data key.</param>
    /// <param name="cancellationToken">
    /// The operation token enforced while the plaintext source is consumed. It is honored on every read because
    /// the file store's copy loop does not surface a token to this stream.
    /// </param>
    public ChunkedAeadEncryptingReadStream(
        Stream plaintext,
        IDataProtector protector,
        CancellationToken cancellationToken)
    {
        _plaintext = plaintext;
        _cancellationToken = cancellationToken;
        _noncePrefix = RandomNumberGenerator.GetBytes(RecordingMediaCryptoFormat.NoncePrefixSizeBytes);
        _plaintextChunk = new byte[RecordingMediaCryptoFormat.ChunkSizeBytes];
        _frameBuffer = new byte[RecordingMediaCryptoFormat.FrameHeaderSizeBytes + RecordingMediaCryptoFormat.ChunkSizeBytes];

        var dataKey = RandomNumberGenerator.GetBytes(RecordingMediaCryptoFormat.KeySizeBytes);

        try
        {
            _aesGcm = new AesGcm(dataKey, RecordingMediaCryptoFormat.TagSizeBytes);
            _header = BuildHeader(protector.Protect(dataKey), _noncePrefix);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
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
        _cancellationToken.ThrowIfCancellationRequested();

        if (!EnsureSegment(out var available) || available == 0)
        {
            return 0;
        }

        return ServeFrom(buffer, available);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        cancellationToken.ThrowIfCancellationRequested();

        var available = await EnsureSegmentAsync(cancellationToken);

        if (available == 0)
        {
            return 0;
        }

        return ServeFrom(buffer.Span, available);
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

    private int ServeFrom(Span<byte> buffer, int available)
    {
        var toCopy = Math.Min(buffer.Length, available);
        _segment.AsSpan(_segmentOffset, toCopy).CopyTo(buffer);
        _segmentOffset += toCopy;

        if (_segmentOffset == _segmentLength && _segmentIsFinalFrame)
        {
            _finalFrameServed = true;
        }

        return toCopy;
    }

    private bool EnsureSegment(out int available)
    {
        available = _segmentLength - _segmentOffset;

        if (available > 0)
        {
            return true;
        }

        if (_finalFrameServed)
        {
            return false;
        }

        if (!_headerServed)
        {
            SetSegment(_header, _header.Length, isFinalFrame: false);
            _headerServed = true;
            available = _segmentLength - _segmentOffset;

            return true;
        }

        var read = ReadPlaintextChunk();
        available = AssembleFrame(read);

        return true;
    }

    private async ValueTask<int> EnsureSegmentAsync(CancellationToken cancellationToken)
    {
        var available = _segmentLength - _segmentOffset;

        if (available > 0)
        {
            return available;
        }

        if (_finalFrameServed)
        {
            return 0;
        }

        if (!_headerServed)
        {
            SetSegment(_header, _header.Length, isFinalFrame: false);
            _headerServed = true;

            return _segmentLength - _segmentOffset;
        }

        var read = await ReadPlaintextChunkAsync(cancellationToken);

        return AssembleFrame(read);
    }

    private int ReadPlaintextChunk()
    {
        var total = 0;

        while (total < _plaintextChunk.Length)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var read = _plaintext.Read(_plaintextChunk.AsSpan(total));

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private async ValueTask<int> ReadPlaintextChunkAsync(CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < _plaintextChunk.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The file store's copy loop reads this stream with no token, so the operation token supplied at
            // construction is what actually cancels the blocking source read on shutdown or an aborted request.
            var read = await _plaintext.ReadAsync(_plaintextChunk.AsMemory(total), _cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private int AssembleFrame(int plaintextLength)
    {
        var isFinal = plaintextLength < _plaintextChunk.Length;
        var flags = isFinal ? RecordingMediaCryptoFormat.FinalFrameFlag : (byte)0;

        Span<byte> nonce = stackalloc byte[RecordingMediaCryptoFormat.NonceSizeBytes];
        RecordingMediaCryptoFormat.BuildNonce(_noncePrefix, _counter, nonce);

        Span<byte> associatedData = stackalloc byte[RecordingMediaCryptoFormat.AssociatedDataSizeBytes];
        RecordingMediaCryptoFormat.BuildAssociatedData(flags, plaintextLength, _counter, associatedData);

        _frameBuffer[0] = flags;
        BinaryPrimitives.WriteInt32LittleEndian(_frameBuffer.AsSpan(1, 4), plaintextLength);

        var tag = _frameBuffer.AsSpan(5, RecordingMediaCryptoFormat.TagSizeBytes);
        var ciphertext = _frameBuffer.AsSpan(RecordingMediaCryptoFormat.FrameHeaderSizeBytes, plaintextLength);

        _aesGcm.Encrypt(nonce, _plaintextChunk.AsSpan(0, plaintextLength), ciphertext, tag, associatedData);
        _counter++;

        var frameLength = RecordingMediaCryptoFormat.FrameHeaderSizeBytes + plaintextLength;
        SetSegment(_frameBuffer, frameLength, isFinal);

        return frameLength;
    }

    private void SetSegment(byte[] segment, int length, bool isFinalFrame)
    {
        _segment = segment;
        _segmentOffset = 0;
        _segmentLength = length;
        _segmentIsFinalFrame = isFinalFrame;
    }

    private static byte[] BuildHeader(byte[] wrappedKey, byte[] noncePrefix)
    {
        var header = new byte[RecordingMediaCryptoFormat.Magic.Length + 1 + 4 + wrappedKey.Length + noncePrefix.Length];
        var offset = 0;

        RecordingMediaCryptoFormat.Magic.CopyTo(header.AsSpan(offset));
        offset += RecordingMediaCryptoFormat.Magic.Length;

        header[offset] = RecordingMediaCryptoFormat.Version;
        offset += 1;

        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(offset, 4), wrappedKey.Length);
        offset += 4;

        wrappedKey.CopyTo(header.AsSpan(offset));
        offset += wrappedKey.Length;

        noncePrefix.CopyTo(header.AsSpan(offset));

        return header;
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
            }
        }

        base.Dispose(disposing);
    }
}

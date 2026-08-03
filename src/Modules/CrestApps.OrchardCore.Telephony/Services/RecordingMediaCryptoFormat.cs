using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Defines the streaming, chunked authenticated-encryption format used by
/// <see cref="LocalEncryptedRecordingMediaStore"/> to encrypt conversation recordings at rest without ever
/// holding a whole recording in memory. The format is envelope encryption: a fresh random data key encrypts
/// the media as a sequence of independently authenticated AES-GCM frames, and that data key is itself wrapped
/// by the data protection provider so key management (tenant isolation, rotation) stays with data protection
/// while the bulk media is streamed a fixed chunk at a time. Every frame binds its ordinal position and the
/// end-of-stream marker into the authenticated associated data, so a tampered, reordered, or truncated file is
/// rejected on read rather than silently returning altered audio.
/// </summary>
internal static class RecordingMediaCryptoFormat
{
    /// <summary>
    /// The file magic identifying a chunked recording-media container (<c>CCR1</c>).
    /// </summary>
    internal static ReadOnlySpan<byte> Magic => "CCR1"u8;

    /// <summary>
    /// The on-disk format version.
    /// </summary>
    internal const byte Version = 1;

    /// <summary>
    /// The size, in bytes, of the AES-256 data key.
    /// </summary>
    internal const int KeySizeBytes = 32;

    /// <summary>
    /// The size, in bytes, of the random nonce prefix that, combined with the per-frame counter, forms a unique
    /// AES-GCM nonce for every frame in a recording.
    /// </summary>
    internal const int NoncePrefixSizeBytes = 4;

    /// <summary>
    /// The size, in bytes, of an AES-GCM nonce.
    /// </summary>
    internal const int NonceSizeBytes = 12;

    /// <summary>
    /// The size, in bytes, of an AES-GCM authentication tag.
    /// </summary>
    internal const int TagSizeBytes = 16;

    /// <summary>
    /// The plaintext size, in bytes, of a single encrypted frame. Read and write peak memory is bounded to a
    /// small multiple of this value regardless of the total recording size.
    /// </summary>
    internal const int ChunkSizeBytes = 64 * 1024;

    /// <summary>
    /// The frame-flags bit that marks the final frame of a recording, allowing truncation to be detected on read.
    /// </summary>
    internal const byte FinalFrameFlag = 0x01;

    /// <summary>
    /// The fixed per-frame header size: one flags byte, a four-byte little-endian plaintext length, and the tag.
    /// </summary>
    internal const int FrameHeaderSizeBytes = 1 + 4 + TagSizeBytes;

    /// <summary>
    /// The authenticated associated-data size bound into every frame: version, flags, plaintext length, counter.
    /// </summary>
    internal const int AssociatedDataSizeBytes = 1 + 1 + 4 + 8;

    /// <summary>
    /// An upper bound on the wrapped-data-key length accepted while reading a container header, guarding against a
    /// corrupt or hostile file that declares an implausibly large key blob.
    /// </summary>
    internal const int MaxWrappedKeyLength = 4096;

    /// <summary>
    /// Creates a readable stream that lazily encrypts <paramref name="plaintext"/> into the chunked container
    /// format as it is read, so the encrypted bytes can be streamed straight to storage.
    /// </summary>
    /// <param name="plaintext">The readable plaintext recording stream. The caller retains ownership of it.</param>
    /// <param name="protector">The data protector used to wrap the per-recording data key.</param>
    /// <param name="cancellationToken">
    /// The operation token honored on every source read; the file store's copy loop does not surface a token to
    /// the returned stream, so this token is what cancels the encryption while the source is being consumed.
    /// </param>
    /// <returns>A readable stream over the encrypted container.</returns>
    internal static Stream CreateEncryptingReadStream(
        Stream plaintext,
        IDataProtector protector,
        CancellationToken cancellationToken)
    {
        return new ChunkedAeadEncryptingReadStream(plaintext, protector, cancellationToken);
    }

    /// <summary>
    /// Opens a readable stream that lazily decrypts a chunked container as it is read. The container header is
    /// read and the data key unwrapped eagerly so an invalid or non-container file fails fast.
    /// </summary>
    /// <param name="ciphertext">The readable container stream. The returned stream takes ownership and disposes it.</param>
    /// <param name="protector">The data protector used to unwrap the per-recording data key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A readable stream over the decrypted recording bytes.</returns>
    internal static async Task<Stream> OpenDecryptingReadStreamAsync(
        Stream ciphertext,
        IDataProtector protector,
        CancellationToken cancellationToken)
    {
        var header = new byte[Magic.Length + 1 + 4];
        await ciphertext.ReadExactlyAsync(header, cancellationToken);

        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new CryptographicException("The recording media container magic is invalid.");
        }

        var version = header[Magic.Length];

        if (version != Version)
        {
            throw new CryptographicException($"The recording media container version '{version}' is not supported.");
        }

        var wrappedKeyLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(Magic.Length + 1, 4));

        if (wrappedKeyLength is <= 0 or > MaxWrappedKeyLength)
        {
            throw new CryptographicException("The recording media container declares an invalid data-key length.");
        }

        var wrappedKey = new byte[wrappedKeyLength];
        await ciphertext.ReadExactlyAsync(wrappedKey, cancellationToken);

        var noncePrefix = new byte[NoncePrefixSizeBytes];
        await ciphertext.ReadExactlyAsync(noncePrefix, cancellationToken);

        var dataKey = protector.Unprotect(wrappedKey);

        try
        {
            if (dataKey.Length != KeySizeBytes)
            {
                throw new CryptographicException("The recording media container data key has an unexpected length.");
            }

            var aesGcm = new AesGcm(dataKey, TagSizeBytes);

            return new ChunkedAeadDecryptingReadStream(ciphertext, aesGcm, noncePrefix);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>
    /// Builds the 12-byte AES-GCM nonce for a frame from the recording's random nonce prefix and the frame's
    /// monotonic counter, guaranteeing a unique nonce per frame under one data key.
    /// </summary>
    /// <param name="noncePrefix">The recording's random nonce prefix.</param>
    /// <param name="counter">The zero-based frame counter.</param>
    /// <param name="destination">The span that receives the nonce; it must be <see cref="NonceSizeBytes"/> long.</param>
    internal static void BuildNonce(ReadOnlySpan<byte> noncePrefix, ulong counter, Span<byte> destination)
    {
        noncePrefix.CopyTo(destination);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(NoncePrefixSizeBytes), counter);
    }

    /// <summary>
    /// Builds the authenticated associated data bound into a frame, binding the frame's flags, plaintext length,
    /// and ordinal position so reordering, truncation, or a flipped end-of-stream marker are all rejected.
    /// </summary>
    /// <param name="flags">The frame flags.</param>
    /// <param name="plaintextLength">The frame plaintext length.</param>
    /// <param name="counter">The zero-based frame counter.</param>
    /// <param name="destination">The span that receives the data; it must be <see cref="AssociatedDataSizeBytes"/> long.</param>
    internal static void BuildAssociatedData(byte flags, int plaintextLength, ulong counter, Span<byte> destination)
    {
        destination[0] = Version;
        destination[1] = flags;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(2, 4), plaintextLength);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(6, 8), counter);
    }
}

using System.Security.Cryptography;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class RecordingMediaCryptoFormatTests
{
    private const int Chunk = RecordingMediaCryptoFormat.ChunkSizeBytes;
    private const int FrameHeader = RecordingMediaCryptoFormat.FrameHeaderSizeBytes;
    private const int FullFrame = FrameHeader + Chunk;

    private readonly IDataProtector _protector = new EphemeralDataProtectionProvider().CreateProtector("recording-media-tests");

    public static TheoryData<int> RecordingLengths =>
    [
        0,
        1,
        Chunk - 1,
        Chunk,
        Chunk + 1,
        Chunk * 3,
        (Chunk * 3) + 517,
    ];

    [Theory]
    [MemberData(nameof(RecordingLengths))]
    public async Task OpenDecryptingReadStreamAsync_WhenSourceSeeks_ReportsLengthAndReadsInOrder(int length)
    {
        // Arrange
        var plaintext = CreatePlaintext(length);
        var container = await EncryptAsync(plaintext);

        // Act
        await using var stream = await OpenAsync(container);
        var readBack = await ReadToEndAsync(stream);

        // Assert
        Assert.True(stream.CanSeek);
        Assert.Equal(length, stream.Length);
        Assert.Equal(length, stream.Position);
        Assert.Equal(plaintext, readBack);
    }

    [Theory]
    [MemberData(nameof(RecordingLengths))]
    public async Task OpenDecryptingReadStreamAsync_WhenSourceCannotSeek_ReadsForwardOnly(int length)
    {
        // Arrange
        var plaintext = CreatePlaintext(length);
        var container = await EncryptAsync(plaintext);

        // Act
        await using var stream = await OpenAsync(new NonSeekableStream(container));
        var readBack = await ReadToEndAsync(stream);

        // Assert
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Equal(plaintext, readBack);
    }

    [Fact]
    public async Task Seek_ToAnyOffset_ReadsThePlaintextAtThatOffset()
    {
        // Arrange
        var plaintext = CreatePlaintext((Chunk * 5) + 1234);
        var container = await EncryptAsync(plaintext);
        var random = new Random(4242);

        await using var stream = await OpenAsync(container);

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var offset = random.Next(plaintext.Length);
            var count = Math.Min(random.Next(1, Chunk * 2), plaintext.Length - offset);
            var buffer = new byte[count];

            // Act
            stream.Position = offset;

            if (attempt % 2 == 0)
            {
                await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);
            }
            else
            {
                stream.ReadExactly(buffer);
            }

            // Assert
            Assert.Equal(plaintext.AsSpan(offset, count).ToArray(), buffer);
            Assert.Equal(offset + count, stream.Position);
        }
    }

    [Theory]
    [InlineData(Chunk - 1, 2)]
    [InlineData(Chunk - 10, 20)]
    [InlineData(Chunk, 1)]
    [InlineData((Chunk * 2) - 1, Chunk + 2)]
    public async Task Read_AcrossFrameBoundaries_ReturnsContiguousPlaintext(int offset, int count)
    {
        // Arrange
        var plaintext = CreatePlaintext((Chunk * 4) + 99);
        var container = await EncryptAsync(plaintext);
        var buffer = new byte[count];

        await using var stream = await OpenAsync(container);

        // Act
        stream.Seek(offset, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(plaintext.AsSpan(offset, count).ToArray(), buffer);
    }

    [Fact]
    public async Task Seek_FromTheEndAndFromTheCurrentPosition_MovesRelativeToThem()
    {
        // Arrange
        var plaintext = CreatePlaintext((Chunk * 2) + 300);
        var container = await EncryptAsync(plaintext);
        var buffer = new byte[100];

        await using var stream = await OpenAsync(container);

        // Act & Assert
        Assert.Equal(plaintext.Length - 100, stream.Seek(-100, SeekOrigin.End));
        await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(plaintext.AsSpan(plaintext.Length - 100).ToArray(), buffer);

        Assert.Equal(plaintext.Length - (Chunk + 100), stream.Seek(-(Chunk + 100), SeekOrigin.Current));
        await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(plaintext.AsSpan(plaintext.Length - (Chunk + 100), 100).ToArray(), buffer);
    }

    [Fact]
    public async Task Read_AtOrPastTheEnd_ReturnsNothing()
    {
        // Arrange
        var plaintext = CreatePlaintext(Chunk + 10);
        var container = await EncryptAsync(plaintext);
        var buffer = new byte[16];

        await using var stream = await OpenAsync(container);

        // Act & Assert
        stream.Seek(0, SeekOrigin.End);
        Assert.Equal(0, await stream.ReadAsync(buffer, TestContext.Current.CancellationToken));

        stream.Seek(Chunk * 10, SeekOrigin.Begin);
        Assert.Equal(0, stream.Read(buffer));
    }

    [Fact]
    public async Task Seek_BeforeTheStart_Throws()
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext(100));

        await using var stream = await OpenAsync(container);

        // Act & Assert
        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek(-101, SeekOrigin.End));
    }

    [Fact]
    public async Task Read_WhenRecordingIsAnExactMultipleOfTheChunk_EndsOnTheEmptyFinalFrame()
    {
        // Arrange
        // A recording that fills its frames exactly ends with an empty final frame.
        var plaintext = CreatePlaintext(Chunk * 2);
        var container = await EncryptAsync(plaintext);
        var lastBytes = new byte[10];

        await using var stream = await OpenAsync(container);

        // Act
        stream.Seek(-10, SeekOrigin.End);
        await stream.ReadExactlyAsync(lastBytes, TestContext.Current.CancellationToken);
        var atEnd = await stream.ReadAsync(new byte[1], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HeaderLength(container) + (FullFrame * 2) + FrameHeader, container.Length);
        Assert.Equal(Chunk * 2, stream.Length);
        Assert.Equal(plaintext.AsSpan(plaintext.Length - 10).ToArray(), lastBytes);
        Assert.Equal(0, atEnd);
    }

    [Fact]
    public async Task Read_ARangeInTheMiddle_DecryptsOnlyTheFramesItCovers()
    {
        // Arrange
        var plaintext = CreatePlaintext((Chunk * 20) + 7);
        var container = await EncryptAsync(plaintext);
        var source = new CountingStream(container);
        var buffer = new byte[1000];

        await using var stream = await OpenAsync(source);
        var readToOpen = source.BytesRead;

        // Act
        stream.Seek((Chunk * 10) + 500, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);

        // Assert
        // Opening reads the container header and authenticates the short final frame; the range reads one frame.
        Assert.Equal(HeaderLength(container) + FrameHeader + 7, readToOpen);
        Assert.Equal(FullFrame, source.BytesRead - readToOpen);
        Assert.Equal(plaintext.AsSpan((Chunk * 10) + 500, 1000).ToArray(), buffer);
    }

    [Fact]
    public async Task OpenDecryptingReadStreamAsync_WhenFinalFrameIsDropped_Throws()
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext((Chunk * 2) + 128));
        var truncated = container.AsSpan(0, container.Length - (FrameHeader + 128)).ToArray();

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() => OpenAsync(truncated));
    }

    [Fact]
    public async Task OpenDecryptingReadStreamAsync_WhenEmptyFinalFrameIsDropped_Throws()
    {
        // Arrange
        // Dropping the empty final frame leaves a container that ends exactly on a frame boundary.
        var container = await EncryptAsync(CreatePlaintext(Chunk * 2));
        var truncated = container.AsSpan(0, container.Length - FrameHeader).ToArray();

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() => OpenAsync(truncated));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(FrameHeader)]
    [InlineData(FrameHeader + 1)]
    [InlineData(Chunk)]
    public async Task OpenDecryptingReadStreamAsync_WhenTruncatedInsideAFrame_Throws(int bytesRemoved)
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext((Chunk * 3) + 4000));
        var truncated = container.AsSpan(0, container.Length - bytesRemoved).ToArray();

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await OpenAsync(truncated);
            await ReadToEndAsync(stream);
        });
    }

    [Fact]
    public async Task OpenDecryptingReadStreamAsync_WhenFramesAreReplacedByAForgedFinalFrame_Throws()
    {
        // Arrange
        // Keep two authentic full frames and end the container with a frame header's worth of bytes, so the length
        // implies a complete recording with an empty final frame. The forged final frame does not authenticate.
        var container = await EncryptAsync(CreatePlaintext((Chunk * 4) + 50));
        var keep = HeaderLength(container) + (FullFrame * 2);
        var forged = new byte[keep + FrameHeader];
        container.AsSpan(0, keep).CopyTo(forged);
        forged[keep] = RecordingMediaCryptoFormat.FinalFrameFlag;

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() => OpenAsync(forged));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(FullFrame)]
    public async Task OpenDecryptingReadStreamAsync_WhenDataIsAppended_Throws(int bytesAppended)
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext(Chunk + 2048));
        var extended = new byte[container.Length + bytesAppended];
        container.CopyTo(extended, 0);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await OpenAsync(extended);
            await ReadToEndAsync(stream);
        });
    }

    [Fact]
    public async Task Read_WhenAFrameIsChanged_ThrowsForThatFrameOnly()
    {
        // Arrange
        var plaintext = CreatePlaintext((Chunk * 3) + 10);
        var container = await EncryptAsync(plaintext);

        // Change one byte in the body of frame 1.
        container[HeaderLength(container) + FullFrame + FrameHeader + 100] ^= 0xFF;

        await using var stream = await OpenAsync(container);
        var buffer = new byte[100];

        // Act & Assert
        stream.Seek(Chunk * 2, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(plaintext.AsSpan(Chunk * 2, 100).ToArray(), buffer);

        stream.Seek(Chunk + 5, SeekOrigin.Begin);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () => await stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<CryptographicException>(() => stream.Read(buffer));
    }

    [Fact]
    public async Task Read_WhenFramesAreSwapped_Throws()
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext((Chunk * 3) + 10));
        var first = HeaderLength(container);
        var frame0 = container.AsSpan(first, FullFrame).ToArray();
        container.AsSpan(first + FullFrame, FullFrame).CopyTo(container.AsSpan(first));
        frame0.CopyTo(container.AsSpan(first + FullFrame));

        await using var stream = await OpenAsync(container);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () => await stream.ReadExactlyAsync(new byte[10], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_WhenSourceCannotSeekAndIsTruncated_Throws()
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext((Chunk * 2) + 128));
        var truncated = container.AsSpan(0, container.Length - (FrameHeader + 128)).ToArray();

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await OpenAsync(new NonSeekableStream(truncated));
            await ReadToEndAsync(stream);
        });
    }

    [Fact]
    public async Task Read_WhenSourceCannotSeekAndHasTrailingData_Throws()
    {
        // Arrange
        var container = await EncryptAsync(CreatePlaintext(2048));
        var extended = new byte[container.Length + 4];
        container.CopyTo(extended, 0);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await OpenAsync(new NonSeekableStream(extended));
            await ReadToEndAsync(stream);
        });
    }

    private async Task<byte[]> EncryptAsync(byte[] plaintext)
    {
        await using var encrypting = RecordingMediaCryptoFormat.CreateEncryptingReadStream(
            new MemoryStream(plaintext, writable: false),
            _protector,
            TestContext.Current.CancellationToken);

        using var container = new MemoryStream();
        await encrypting.CopyToAsync(container, TestContext.Current.CancellationToken);

        return container.ToArray();
    }

    private Task<Stream> OpenAsync(byte[] container)
    {
        return OpenAsync(new MemoryStream(container, writable: false));
    }

    private Task<Stream> OpenAsync(Stream source)
    {
        return RecordingMediaCryptoFormat.OpenDecryptingReadStreamAsync(source, _protector, TestContext.Current.CancellationToken);
    }

    private static async Task<byte[]> ReadToEndAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        return buffer.ToArray();
    }

    private static byte[] CreatePlaintext(int length)
    {
        var plaintext = new byte[length];
        new Random(length).NextBytes(plaintext);

        return plaintext;
    }

    private static int HeaderLength(byte[] container)
    {
        // Magic, version, wrapped key length, wrapped key, nonce prefix.
        var wrappedKeyLength = BitConverter.ToInt32(container, RecordingMediaCryptoFormat.Magic.Length + 1);

        return RecordingMediaCryptoFormat.Magic.Length + 1 + 4 + wrappedKeyLength + RecordingMediaCryptoFormat.NoncePrefixSizeBytes;
    }

    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    private sealed class CountingStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public long BytesRead { get; private set; }

        // A derived MemoryStream serves its span and async reads through this overload, so counting here counts
        // every read once.
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;

            return read;
        }
    }
}

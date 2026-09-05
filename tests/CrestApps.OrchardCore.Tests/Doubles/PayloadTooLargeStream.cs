using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Doubles;

public sealed class PayloadTooLargeStream : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw CreateException();
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromException<int>(CreateException());
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private static BadHttpRequestException CreateException()
    {
        return new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
    }
}

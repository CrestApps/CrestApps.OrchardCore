using System.Buffers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// Reads a request body that an untrusted caller controls, refusing to hold more of it in memory than the caller is
/// allowed to send.
/// </summary>
public static class RequestBodyReader
{
    private const int InitialBufferSize = 8 * 1024;

    /// <summary>
    /// Reads the request body as text, refusing it as soon as it exceeds the allowance rather than after it has
    /// already been buffered.
    /// </summary>
    /// <param name="request">The request whose body is read.</param>
    /// <param name="maximumBytes">The largest body, in bytes, the caller is allowed to send.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The body the caller sent, or a result reporting that the caller sent more than it is allowed to.</returns>
    public static async ValueTask<RequestBodyReadResult> ReadAsync(
        HttpRequest request,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, int.MaxValue);

        if (request.ContentLength > maximumBytes)
        {
            return RequestBodyReadResult.TooLarge;
        }

        // A chunked request declares no length at all, so a caller that wants to send more than it is allowed to
        // simply omits the header. Handing the ceiling to the server as well stops those bytes before they reach
        // this process on the hosts that support it, and the loop below enforces it on the hosts that do not.
        var sizeFeature = request.HttpContext?.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is not null && !sizeFeature.IsReadOnly)
        {
            sizeFeature.MaxRequestBodySize = maximumBytes;
        }

        var reader = request.BodyReader;
        var capacity = request.ContentLength is > 0 && request.ContentLength <= maximumBytes
            ? (int)request.ContentLength.Value
            : (int)Math.Min(InitialBufferSize, maximumBytes);

        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(capacity, 1));
        var written = 0;

        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(cancellationToken);

                var arrived = read.Buffer;

                if (read.IsCanceled)
                {
                    reader.AdvanceTo(arrived.Start, arrived.End);

                    throw new OperationCanceledException(cancellationToken);
                }


                if (written + arrived.Length > maximumBytes)
                {
                    // The body is refused before it is ever turned into a string, so an oversized caller costs
                    // this process the bytes it takes to notice rather than the bytes the caller chose to send.
                    reader.AdvanceTo(arrived.End);

                    return RequestBodyReadResult.TooLarge;
                }

                var length = (int)arrived.Length;

                if (length > 0)
                {
                    if (written + length > buffer.Length)
                    {
                        Grow(ref buffer, written, written + length);
                    }

                    arrived.CopyTo(buffer.AsSpan(written));
                    written += length;
                }

                // Every byte that arrived is consumed on the way past. Leaving it unconsumed and asking only for
                // more stalls the server's own body pump, which pauses its writer until the reader drains it.
                reader.AdvanceTo(arrived.End);

                if (read.IsCompleted)
                {
                    return RequestBodyReadResult.FromBody(Encoding.UTF8.GetString(buffer, 0, written));
                }
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // The ceiling handed to the server above means the server can be the one that notices. Callers of this
            // method should not have to tell the two refusals apart, so the server's refusal is reported the same
            // way as the one the loop makes.
            return RequestBodyReadResult.TooLarge;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void Grow(ref byte[] buffer, int written, int required)
    {
        var enlarged = ArrayPool<byte>.Shared.Rent(Math.Max(required, buffer.Length * 2));

        buffer.AsSpan(0, written).CopyTo(enlarged);
        ArrayPool<byte>.Shared.Return(buffer);

        buffer = enlarged;
    }
}

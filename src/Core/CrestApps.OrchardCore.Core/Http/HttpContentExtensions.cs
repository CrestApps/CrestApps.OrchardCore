namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// Reads response bodies from hosts the site does not control.
/// </summary>
public static class HttpContentExtensions
{
    private const int ChunkSize = 81920;

    /// <summary>
    /// Reads a body up to a limit, and stops as soon as it goes over. A declared length can be missing or wrong (a chunked
    /// answer declares none), so the read itself is held to the limit rather than trusting <c>Content-Length</c>.
    /// </summary>
    /// <param name="content">The body.</param>
    /// <param name="maxBytes">The most bytes accepted.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when the body is longer than <paramref name="maxBytes"/>.</returns>
    public static async Task<byte[]> ReadBoundedAsync(this HttpContent content, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        if (content.Headers.ContentLength is long declared && declared > maxBytes)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        int read;

        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

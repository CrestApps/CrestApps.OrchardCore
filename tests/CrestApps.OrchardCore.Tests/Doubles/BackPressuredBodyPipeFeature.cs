using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Presents a request body through a pipe that refuses to accept another write until the reader has consumed what it
/// was already given, which is how the server feeds a request whose length is not declared up front.
/// </summary>
public sealed class BackPressuredBodyPipeFeature : IRequestBodyPipeFeature
{
    private readonly Pipe _pipe = new(new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1, useSynchronizationContext: false));

    /// <summary>
    /// Gets the reader the request body is read through.
    /// </summary>
    public PipeReader Reader
    {
        get => _pipe.Reader;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Writes the payload one piece at a time, waiting after each piece for the reader to make room, and then reports
    /// that no more of the body is coming.
    /// </summary>
    /// <param name="payload">The body to send.</param>
    /// <param name="pieceSize">The number of bytes to send at a time.</param>
    /// <returns>A task that completes once the whole payload has been accepted by the reader.</returns>
    public async Task SendAsync(byte[] payload, int pieceSize)
    {
        ArgumentNullException.ThrowIfNull(payload);

        for (var offset = 0; offset < payload.Length; offset += pieceSize)
        {
            var length = Math.Min(pieceSize, payload.Length - offset);

            await _pipe.Writer.WriteAsync(payload.AsMemory(offset, length));
        }

        await _pipe.Writer.CompleteAsync();
    }
}

using System.Text;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CrestApps.OrchardCore.Tests.Core.Http;

/// <summary>
/// Covers the reading of a request body that an untrusted caller controls.
/// </summary>
public sealed class RequestBodyReaderTests
{
    private const long Allowance = 64 * 1024;

    /// <summary>
    /// A caller that wants to send more than it is allowed to simply omits its content length, so a limit checked
    /// against the declared length is a limit the caller opts into. What arrives has to be measured as it arrives,
    /// or an endpoint that advertises a ceiling holds whatever the caller decided to send.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenTheBodyDeclaresNoLength_RefusesItAtTheAllowance()
    {
        // Arrange
        var body = new EndlessStream();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = body;

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsTooLarge);
        Assert.Null(result.Body);

        Assert.True(
            body.BytesProduced < Allowance * 2,
            $"The reader took {body.BytesProduced} bytes of a body it is not willing to accept.");
    }

    /// <summary>
    /// A body larger than one buffer arrives in pieces, and a reader that only decoded the first piece would hand
    /// the endpoint a truncated payload that still parses often enough to be dangerous.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenTheBodyArrivesInSeveralPieces_ReturnsAllOfIt()
    {
        // Arrange
        var payload = string.Concat(Enumerable.Repeat("crestapps", 4096));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsTooLarge);
        Assert.Equal(payload, result.Body);
    }

    /// <summary>
    /// A caller that declares more than it is allowed to send is refused before any of it is read, so the cheapest
    /// rejection stays the cheapest rejection.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenTheBodyDeclaresMoreThanTheAllowance_RefusesItWithoutReadingIt()
    {
        // Arrange
        var body = new EndlessStream();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = body;
        httpContext.Request.ContentLength = Allowance + 1;

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsTooLarge);
        Assert.Equal(0, body.BytesProduced);
    }

    /// <summary>
    /// The server pauses the caller's upload until the bytes it has already handed over are consumed. A reader that
    /// asks for more without consuming what it has stalls the request until the server abandons it, which turns a
    /// perfectly ordinary chunked delivery into a hung request.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenTheServerWaitsForEachPieceToBeConsumed_StillReadsTheWholeBody()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("crestapps", 2048)));
        var feature = new BackPressuredBodyPipeFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IRequestBodyPipeFeature>(feature);

        var sending = feature.SendAsync(payload, pieceSize: 512);

        // Act
        var reading = RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken).AsTask();
        var result = await reading.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await sending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsTooLarge);
        Assert.Equal(Encoding.UTF8.GetString(payload), result.Body);
    }

    /// <summary>
    /// The ceiling is handed to the server as well, so on a real host the server is usually the one that refuses an
    /// oversized body. Callers of this reader should not have to tell the two refusals apart.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenTheServerRefusesTheBody_ReportsItAsTooLargeRatherThanThrowing()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new RefusedStream();

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsTooLarge);
        Assert.Null(result.Body);
    }

    /// <summary>
    /// The allowance is the largest body the caller is allowed to send, not the smallest one it is refused for, so
    /// the boundary is pinned in both directions.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public async Task ReadAsync_AtTheBoundaryOfTheAllowance_RefusesOnlyWhatExceedsIt(int offset, bool expectedTooLarge)
    {
        // Arrange
        var payload = new string('c', (int)Allowance + offset);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedTooLarge, result.IsTooLarge);

        if (!expectedTooLarge)
        {
            Assert.Equal(payload, result.Body);
        }
    }

    /// <summary>
    /// A body is split wherever the network happened to split it, which can fall in the middle of a character. A
    /// reader that decoded each piece on its own would replace that character with a substitute and hand the
    /// endpoint a payload the caller never sent.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenACharacterIsSplitBetweenPieces_DecodesItWhole()
    {
        // Arrange
        var payload = string.Concat(Enumerable.Repeat("caf\u00e9 \u0645\u0631\u062d\u0628\u0627 \ud83d\ude80", 256));
        var bytes = Encoding.UTF8.GetBytes(payload);
        var feature = new BackPressuredBodyPipeFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IRequestBodyPipeFeature>(feature);

        var sending = feature.SendAsync(bytes, pieceSize: 7);

        // Act
        var result = await RequestBodyReader.ReadAsync(httpContext.Request, Allowance, TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await sending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsTooLarge);
        Assert.Equal(payload, result.Body);
    }
}

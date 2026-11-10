using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// An endpoint's idempotency policy decides its own answers: what a request without a key gets, that a retry of the
/// same request is replayed and marked as such, that the same key with another request is a conflict, and whether
/// the key is echoed back.
/// </summary>
public sealed class IdempotencyPolicyTests
{
    private const string KeyHeader = "Idempotency-Key";
    private const string FingerprintHeader = "X-Request-Hash";
    private const string ReplayedHeader = "Idempotent-Replayed";

    private static readonly IResult _missingKey = TypedResults.BadRequest("An idempotency key is required.");
    private static readonly IResult _conflict = TypedResults.UnprocessableEntity("The key was used for another request.");

    private readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())))
        .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
        .BuildServiceProvider();

    private readonly IdempotencyPolicy _policy = new()
    {
        KeyHeader = KeyHeader,
        MissingKey = _ => _missingKey,
        Conflict = _ => _conflict,
        Fingerprint = httpContext => httpContext.Request.Headers[FingerprintHeader].ToString(),
        ReplayedHeader = ReplayedHeader,
        EchoKey = true,
    };

    private int _calls;

    [Fact]
    public async Task ARequestWithoutAKey_GetsThePolicysMissingKeyAnswer()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var request = Request(key: null, fingerprint: "a");

        // Act
        var result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(request), NextAsync);

        // Assert
        Assert.Same(_missingKey, result);
        Assert.Equal(0, _calls);
    }

    [Fact]
    public async Task ARetryOfTheSameRequest_IsReplayedAndMarked()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var first = Request("key-1", "a");

        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(first), NextAsync);

        var retry = Request("key-1", "a");

        // Act
        var result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(retry), NextAsync);

        await Assert.IsAssignableFrom<IResult>(result).ExecuteAsync(retry);

        // Assert
        Assert.Equal(1, _calls);
        Assert.Equal("true", retry.Response.Headers[ReplayedHeader].ToString());
        Assert.Equal(StatusCodes.Status200OK, retry.Response.StatusCode);
        Assert.Equal("{\"id\":\"1\"}", ReadBody(retry));
        Assert.False(first.Response.Headers.ContainsKey(ReplayedHeader));
    }

    [Fact]
    public async Task TheSameKeyWithAnotherRequest_GetsThePolicysConflictAnswer()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();

        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("key-1", "a")), NextAsync);

        var other = Request("key-1", "b");

        // Act
        var result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(other), NextAsync);

        // Assert
        Assert.Same(_conflict, result);
        Assert.Equal(1, _calls);
        Assert.False(other.Response.Headers.ContainsKey(ReplayedHeader));
    }

    [Fact]
    public async Task EchoKey_WritesTheKeyBackOnTheAnswer()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var request = Request("key-1", "a");

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(request), NextAsync);

        // Assert
        Assert.Equal("key-1", request.Response.Headers[KeyHeader].ToString());
        Assert.Equal(1, _calls);
    }

    [Fact]
    public async Task WithoutEchoKey_TheKeyIsNotWrittenBack()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var request = Request("key-1", "a", new IdempotencyPolicy { KeyHeader = KeyHeader });

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(request), NextAsync);

        // Assert
        Assert.False(request.Response.Headers.ContainsKey(KeyHeader));
    }

    private ValueTask<object> NextAsync(EndpointFilterInvocationContext context)
    {
        _calls++;

        return ValueTask.FromResult<object>(TypedResults.Ok(new { id = "1" }));
    }

    private DefaultHttpContext Request(string key, string fingerprint, IdempotencyPolicy policy = null)
    {
        var httpContext = new DefaultHttpContext { RequestServices = _services };

        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(policy ?? _policy), "test"));
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/ucp/v1/checkout-sessions";
        httpContext.Request.Headers[CommerceApi.CartTokenHeader] = "cart-1";
        httpContext.Request.Headers[FingerprintHeader] = fingerprint;
        httpContext.Response.Body = new MemoryStream();

        if (key is not null)
        {
            httpContext.Request.Headers[KeyHeader] = key;
        }

        return httpContext;
    }

    private static string ReadBody(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        using var reader = new StreamReader(httpContext.Response.Body);

        return reader.ReadToEnd();
    }
}

using System.Net;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.DataProtection;
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
/// SEC-C17l: the commerce API's idempotency keys answer only the same request from the same caller. A key sent with
/// another body is refused instead of replaying the first answer; stored answers (which can hold client secrets and guest
/// tokens) are kept encrypted; and callers with no identity don't share one scope.
/// </summary>
public sealed class IdempotencyHardeningTests
{
    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IServiceProvider _services;

    public IdempotencyHardeningTests()
    {
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDistributedCache>(_cache)
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
            .BuildServiceProvider();
    }

    [Fact]
    public async Task TheSameKeyWithAnotherBody_IsRefused()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { refunded = 10 }));
        }

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 10m }), Next);
        var other = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 500m }), Next);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, IdempotencyEndpointFilter.Capture(other).StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TheSameKeyWithAnotherBody_IsRefusedWithACodeThatSaysNotToRetry()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();

        static ValueTask<object> Next(EndpointFilterInvocationContext context)
            => ValueTask.FromResult<object>(TypedResults.Ok(new { refunded = 10 }));

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 10m }), Next);
        var other = IdempotencyEndpointFilter.Capture(await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 500m }), Next));

        // Assert: not the in-progress code, which a client retries.
        Assert.Contains($"\"code\":\"{ApiProblems.Codes.IdempotencyKeyReused}\"", other.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiProblems.Codes.IdempotencyInProgress, other.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequestWhoseArgumentsCantBeCompared_NeverGetsAnotherRequestsAnswer()
    {
        // Arrange: arguments that can't be serialized, so they can't be fingerprinted.
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { refunded = calls }));
        }

        var first = new CyclicRequest { Amount = 10m };
        first.Self = first;
        var second = new CyclicRequest { Amount = 500m };
        second.Self = second;

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), first), Next);
        var other = IdempotencyEndpointFilter.Capture(await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), second), Next));

        // Assert: refused, not the first request's answer replayed.
        Assert.Equal(StatusCodes.Status409Conflict, other.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TheSameKeyWithTheSameBody_GetsTheFirstAnswer()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { refunded = 10 }));
        }

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 10m }), Next);
        var retry = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request("cart-1"), new RefundRequest { Amount = 10m }), Next);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, IdempotencyEndpointFilter.Capture(retry).StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AStoredAnswer_IsNotKeptInClear()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var httpContext = Request("cart-1");

        ValueTask<object> Next(EndpointFilterInvocationContext context)
            => ValueTask.FromResult<object>(TypedResults.Ok(new { clientSecret = "pi_123_secret_456" }));

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), Next);

        // Assert
        var stored = await _cache.GetStringAsync(IdempotencyEndpointFilter.GetCacheKey(httpContext, "key-1"), TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.DoesNotContain("pi_123_secret_456", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoCallersWithNoIdentity_DontShareAnAnswer()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { checkoutToken = "token-" + calls }));
        }

        var first = Request(cartToken: null, address: "203.0.113.10");
        var second = Request(cartToken: null, address: "198.51.100.20");

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(first), Next);
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(second), Next);

        // Assert
        Assert.Equal(2, calls);
    }

    private DefaultHttpContext Request(string cartToken, string address = "203.0.113.10")
    {
        var httpContext = new DefaultHttpContext { RequestServices = _services };

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/commerce/v1/checkout";
        httpContext.Request.Headers[CommerceApi.IdempotencyKeyHeader] = "key-1";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);

        if (cartToken is not null)
        {
            httpContext.Request.Headers[CommerceApi.CartTokenHeader] = cartToken;
        }

        return httpContext;
    }

    public sealed class RefundRequest
    {
        public decimal Amount { get; set; }
    }

    public sealed class CyclicRequest
    {
        public decimal Amount { get; set; }

        public CyclicRequest Self { get; set; }
    }
}

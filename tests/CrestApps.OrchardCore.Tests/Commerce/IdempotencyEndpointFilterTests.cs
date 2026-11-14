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
/// A retry of a request that created something gets the first answer back, with where the new thing is, which only
/// shows once the created result has written its headers.
/// </summary>
public sealed class IdempotencyEndpointFilterTests
{
    private readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())))
        .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
        .BuildServiceProvider();

    [Fact]
    public async Task ARetryOfACreatedAnswer_GetsTheLocationTheFirstAnswerWrote()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Created("/api/commerce/v1/orders/1", new { id = "1" }));
        }

        var first = Request();

        // Act
        var firstResult = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(first), Next);
        await Assert.IsAssignableFrom<IResult>(firstResult).ExecuteAsync(first);

        var retry = Request();
        var retryResult = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(retry), Next);
        await Assert.IsAssignableFrom<IResult>(retryResult).ExecuteAsync(retry);

        // Assert
        Assert.Equal(1, calls);
        Assert.IsAssignableFrom<INestedHttpResult>(firstResult);
        Assert.Equal(StatusCodes.Status201Created, retry.Response.StatusCode);
        Assert.Equal("/api/commerce/v1/orders/1", retry.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ARetryWhileTheCreatedAnswerIsStillRunning_IsRefused()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();

        ValueTask<object> Next(EndpointFilterInvocationContext context)
            => ValueTask.FromResult<object>(TypedResults.Created("/api/commerce/v1/orders/1", new { id = "1" }));

        // Act
        var firstResult = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request()), Next);
        var retryResult = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(Request()), Next);

        // Assert
        Assert.IsAssignableFrom<INestedHttpResult>(firstResult);
        Assert.Equal(StatusCodes.Status409Conflict, IdempotencyEndpointFilter.Capture(retryResult).StatusCode);
    }

    /// <summary>
    /// L-checkout-idempotency-scope (final review): the caller was the user id, else the raw cart or checkout token, else
    /// the network, with nothing saying which. An anonymous request whose cart token was a user's id landed in that user's
    /// scope and was answered with the user's stored response, before any ownership check.
    /// </summary>
    [Fact]
    public async Task AGuestWhoseCartTokenIsAUsersId_IsNotAnsweredFromThatUsersRequests()
    {
        // Arrange
        var filter = new IdempotencyEndpointFilter();
        var calls = 0;

        ValueTask<object> Next(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { id = "order-of-user-1" }));
        }

        var user = Request();
        user.Request.Headers.Remove(CommerceApi.CartTokenHeader);
        user.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-1")], "test"));

        var guest = Request();
        guest.Request.Headers[CommerceApi.CartTokenHeader] = "user-1";

        // Act
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(user), Next);
        await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(guest), Next);

        // Assert
        Assert.Equal(2, calls);
    }

    private DefaultHttpContext Request()
    {
        var httpContext = new DefaultHttpContext { RequestServices = _services };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/commerce/v1/orders";
        httpContext.Request.Headers[CommerceApi.IdempotencyKeyHeader] = "key-1";
        httpContext.Request.Headers[CommerceApi.CartTokenHeader] = "cart-1";
        httpContext.Response.Body = new MemoryStream();

        return httpContext;
    }
}

using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Core.Services;
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
/// What every commerce API endpoint relies on: a guest's cart and checkout travel in headers, never cookies, and a
/// request sent again with the same idempotency key gets the first answer instead of doing the work twice.
/// </summary>
public sealed class CommerceApiFoundationTests
{
    private static readonly GuestSessionTokenScope _scope = new("cart_owner", "CrestApps.Tests.Api.v1", TimeSpan.FromDays(1), MaxSessions: 1, HeaderName: CommerceApi.CartTokenHeader);

    [Fact]
    public void AnApiEndpoint_IssuesTheGuestTokenInAHeader_AndAcceptsItBack_WithoutAnyCookie()
    {
        var protection = new EphemeralDataProtectionProvider();
        var issuing = ApiContext();

        var hash = Manager(protection, issuing).Issue(_scope, "cart-1");
        var token = issuing.Response.Headers[CommerceApi.CartTokenHeader].ToString();

        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(issuing.Response.Headers.ContainsKey("Set-Cookie"));

        var returning = ApiContext();
        returning.Request.Headers[CommerceApi.CartTokenHeader] = token;

        Assert.True(Manager(protection, returning).Verify(_scope, "cart-1", hash));

        // A browser page doesn't read the header: it only trusts its cookie.
        var page = new DefaultHttpContext();
        page.Request.Headers[CommerceApi.CartTokenHeader] = token;

        Assert.False(Manager(protection, page).Verify(_scope, "cart-1", hash));
    }

    [Fact]
    public async Task ARequestSentAgainWithTheSameKey_GetsTheFirstAnswer_AndTheWorkIsDoneOnce()
    {
        var calls = 0;
        var services = Services();

        EndpointFilterDelegate handler = _ =>
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Created("/orders/1", new { Number = "1001", Call = calls }));
        };

        var firstInvocation = Invocation(services, "key-1");
        var first = await new IdempotencyEndpointFilter().InvokeAsync(firstInvocation, handler);

        // A created answer is kept once it has run and written where the new thing is.
        await Assert.IsAssignableFrom<IResult>(first).ExecuteAsync(firstInvocation.HttpContext);

        var second = await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "key-1"), handler);
        var other = await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "key-2"), handler);

        Assert.Equal(2, calls);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(first);

        var replay = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult>(second);
        Assert.Equal(StatusCodes.Status201Created, replay.StatusCode);
        Assert.Contains("\"call\":1", replay.ResponseContent, StringComparison.Ordinal);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(other);
    }

    [Fact]
    public async Task ARequestWithoutAKey_AlwaysRuns()
    {
        var calls = 0;
        var services = Services();

        EndpointFilterDelegate handler = _ =>
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok());
        };

        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, key: null), handler);
        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, key: null), handler);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void AKey_IsKeptPerCallerAndRoute()
    {
        var first = new DefaultHttpContext();
        first.Request.Method = "POST";
        first.Request.Path = "/api/commerce/v1/cart/checkout";
        first.Request.Headers[CommerceApi.CartTokenHeader] = "cart-a";

        var second = new DefaultHttpContext();
        second.Request.Method = "POST";
        second.Request.Path = "/api/commerce/v1/cart/checkout";
        second.Request.Headers[CommerceApi.CartTokenHeader] = "cart-b";

        Assert.NotEqual(IdempotencyEndpointFilter.GetCacheKey(first, "same"), IdempotencyEndpointFilter.GetCacheKey(second, "same"));
    }

    [Fact]
    public void ARetry_CarryingTheCheckoutTokenTheFirstAnswerHandedOut_IsTheSameRequest()
    {
        var first = new DefaultHttpContext();
        first.Request.Method = "POST";
        first.Request.Path = "/api/commerce/v1/checkout";
        first.Request.Headers[CommerceApi.CartTokenHeader] = "cart-a";

        var retry = new DefaultHttpContext();
        retry.Request.Method = "POST";
        retry.Request.Path = "/api/commerce/v1/checkout";
        retry.Request.Headers[CommerceApi.CartTokenHeader] = "cart-a";
        retry.Request.Headers[CommerceApi.CheckoutTokenHeader] = "checkout-handed-out";

        Assert.Equal(IdempotencyEndpointFilter.GetCacheKey(first, "same"), IdempotencyEndpointFilter.GetCacheKey(retry, "same"));
    }

    private static DefaultHttpContext ApiContext()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(GuestSessionTokenHeaderTransport.Instance), "api"));

        return context;
    }

    private static GuestSessionTokenManager Manager(IDataProtectionProvider protection, HttpContext context)
        => new(new HttpContextAccessor { HttpContext = context }, protection);

    private static ServiceProvider Services()
        => new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())))
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .BuildServiceProvider();

    private static EndpointFilterInvocationContext Invocation(IServiceProvider services, string key)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/commerce/v1/cart/checkout";

        if (key is not null)
        {
            context.Request.Headers[CommerceApi.IdempotencyKeyHeader] = key;
        }

        return EndpointFilterInvocationContext.Create(context);
    }
}

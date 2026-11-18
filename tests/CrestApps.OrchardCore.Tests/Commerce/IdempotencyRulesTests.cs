using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The rules of idempotent writes now that they are shared: a caller the request doesn't identify never shares another's
/// answer, an answer the cache can't keep is still sent, a request the caller must correct isn't kept, a reused key is
/// told apart from one still running, and the names the commerce API was built with (its route, scheme, guest headers
/// and cache keys) stay as they were.
/// </summary>
public sealed class IdempotencyRulesTests
{
    [Fact]
    public async Task ARequestThatIdentifiesNoCaller_RunsEachTime_AndKeepsNothing()
    {
        // Arrange: two strangers, no user and no guest token, send the same key.
        var services = Services(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var calls = 0;

        // Act
        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, caller: null), Handler);
        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, caller: null), Handler);

        // Assert
        Assert.Equal(2, calls);

        ValueTask<object> Handler(EndpointFilterInvocationContext context)
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { calls }));
        }
    }

    [Fact]
    public async Task AnAnswerTheCacheCantKeep_IsStillSent()
    {
        // Arrange
        var services = Services(new FailingCache());

        // Act
        var result = await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "cart-1"), _ => ValueTask.FromResult<object>(TypedResults.Ok(new { done = true })));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ARequestTheCallerMustCorrect_IsntKept_SoTheCorrectedRetryRuns()
    {
        // Arrange
        var services = Services(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var calls = 0;

        // Act
        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "cart-1"), _ =>
        {
            calls++;

            return ValueTask.FromResult<object>(ApiProblems.Validation("quantity", "Say how many."));
        });
        var corrected = await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "cart-1"), _ =>
        {
            calls++;

            return ValueTask.FromResult<object>(TypedResults.Ok(new { done = true }));
        });

        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(corrected).StatusCode);
    }

    [Fact]
    public async Task AKeySentAgainWithAnotherRequest_IsToldApartFromOneStillRunning()
    {
        // Arrange
        var services = Services(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var policy = new IdempotencyPolicy { Fingerprint = context => context.Request.Headers["X-Body"].ToString() };

        // Act
        await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "cart-1", policy, body: "a"), _ => ValueTask.FromResult<object>(TypedResults.Ok()));
        var reused = await new IdempotencyEndpointFilter().InvokeAsync(Invocation(services, "cart-1", policy, body: "b"), _ => ValueTask.FromResult<object>(TypedResults.Ok()));

        // Assert
        var stored = IdempotencyEndpointFilter.Capture(reused);
        Assert.Equal(StatusCodes.Status409Conflict, stored.StatusCode);
        Assert.Contains(ApiProblems.Codes.IdempotencyKeyReused, stored.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommerceApisNames_StayAsTheyWere()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/commerce/v1/cart/checkout";
        context.Request.Headers[CommerceApi.CartTokenHeader] = "cart-1";
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(CommerceApi.GuestHeaders), "commerce"));

        // Act
        var requestKey = IdempotencyEndpointFilter.GetCacheKey(context, "key-1");
        var operationKey = IdempotentOperation.GetCacheKey("scope", "key-1");

        // Assert: the key keeps its prefix, with each kind of caller in a scope of its own (a guest's token never shares a
        // user's scope), and apps keep their route, scheme and headers.
        Assert.Equal("commerce-api-idempotency:" + Hash("|POST|/api/commerce/v1/cart/checkout|guest:cart-1|key-1"), requestKey);
        Assert.Equal("commerce-idempotent-operation:" + Hash("scope|key-1"), operationKey);
        Assert.Equal("api/commerce/v1", CommerceApi.RoutePrefix);
        Assert.Equal("Api", CommerceApi.AuthenticationScheme);
        Assert.Equal([CommerceApi.CartTokenHeader, CommerceApi.CheckoutTokenHeader], CommerceApi.GuestHeaders.Headers);
    }

    [Fact]
    public void EveryCommerceEndpoint_CarriesTheGuestHeaders()
    {
        // Arrange
        var app = WebApplication.CreateSlimBuilder().Build();

        // Act
        app.MapCommerceApi("Test").MapPost("orders", () => Results.Ok());
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).Single();

        // Assert
        Assert.Same(CommerceApi.GuestHeaders, endpoint.Metadata.GetMetadata<IdempotencyGuestHeaders>());
        Assert.Equal("api/commerce/v1/orders", ((RouteEndpoint)endpoint).RoutePattern.RawText);
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ServiceProvider Services(IDistributedCache cache)
        => new ServiceCollection()
            .AddLogging()
            .AddSingleton(cache)
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .BuildServiceProvider();

    private static EndpointFilterInvocationContext Invocation(IServiceProvider services, string caller, IdempotencyPolicy policy = null, string body = null)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/commerce/v1/cart/checkout";
        context.Request.Headers[CommerceApi.IdempotencyKeyHeader] = "key-1";
        context.Request.Headers["X-Body"] = body;

        if (caller is not null)
        {
            context.Request.Headers[CommerceApi.CartTokenHeader] = caller;
        }

        var metadata = policy is null
            ? new EndpointMetadataCollection(CommerceApi.GuestHeaders)
            : new EndpointMetadataCollection(CommerceApi.GuestHeaders, policy);

        context.SetEndpoint(new Endpoint(null, metadata, "commerce"));

        return EndpointFilterInvocationContext.Create(context);
    }

    private sealed class FailingCache : IDistributedCache
    {
        public byte[] Get(string key) => null;

        public Task<byte[]> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]>(null);

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => throw new IOException("The cache is down.");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => throw new IOException("The cache is down.");
    }
}

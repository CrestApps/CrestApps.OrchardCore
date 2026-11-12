using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// SEC-A5 (a): the completion API calls the model with no ceiling, and a browser key is public by design, so every key
/// is held to a quota.
/// </summary>
public sealed class AICompletionQuotaFilterTests
{
    [Fact]
    public async Task InvokeAsync_AfterTheKeysQuota_Answers429WithoutCallingTheModel()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(AICompletionQuotaFilter.PolicyName, "key:key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(5)));

        var httpContext = CreateContext(limiter.Object, new Claim(AIApiKeys.KeyClaimType, "key-1"));
        var called = false;

        await new AICompletionQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
        Assert.Equal("300", httpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithinTheQuota_CallsTheEndpoint()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(AICompletionQuotaFilter.PolicyName, "key:key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(true, 10, TimeSpan.Zero));

        var httpContext = CreateContext(limiter.Object, new Claim(AIApiKeys.KeyClaimType, "key-1"));
        var called = false;

        await new AICompletionQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.True(called);
        limiter.Verify(l => l.AcquireAsync(AICompletionQuotaFilter.PolicyName, "key:key-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetQuotaKey_ForAUserWithoutAKey_CountsByUser()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Api"));

        Assert.Equal("user:user-1", AICompletionQuotaFilter.GetQuotaKey(user));
    }

    /// <summary>
    /// A caller with neither an API key nor a user id (an endpoint that lets anonymous callers through) is still held to
    /// a quota, counted by its network.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ForACallerWithoutAKeyOrAUser_CountsByNetwork()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(AICompletionQuotaFilter.PolicyName, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(5)));

        var httpContext = CreateContext(limiter.Object);
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        var called = false;

        await new AICompletionQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        limiter.Verify(l => l.AcquireAsync(AICompletionQuotaFilter.PolicyName, "net:203.0.113.7", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DefaultHttpContext CreateContext(IAttemptLimiter limiter, params Claim[] claims)
        => new()
        {
            RequestServices = new ServiceCollection().AddSingleton(limiter).BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "AIApiKey")),
        };
}

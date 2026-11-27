using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// Round 3 (Medium, AI): the AG-UI endpoint only required an authenticated API caller, so a key scoped to another area
/// of the AI API could chat with every profile and run its tools, with no quota of model calls. It is the chat area now,
/// held to the same scope and quota as the completion routes.
/// </summary>
public sealed class AGUIEndpointScopeAndQuotaTests
{
    [Fact]
    public async Task AKeyScopedToAnotherArea_IsRefused_BeforeTheModelRuns()
    {
        // Arrange
        var limiter = new Mock<IAttemptLimiter>();
        var httpContext = CreateContext(limiter.Object, new Claim(AIApiKeys.KeyClaimType, "key-1"), new Claim(AIApiKeys.ScopeClaimType, "ai." + AIApiKeys.Scopes.Profiles));

        // Act
        await BuildEndpoint().RequestDelegate(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        limiter.Verify(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AChatKey_IsHeldToTheCompletionQuota()
    {
        // Arrange: the key has used its quota, so the run never reaches the hub.
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(value => value.AcquireAsync(AICompletionQuotaFilter.PolicyName, "key:key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(5)));
        var httpContext = CreateContext(limiter.Object, new Claim(AIApiKeys.KeyClaimType, "key-1"), new Claim(AIApiKeys.ScopeClaimType, "ai." + AIApiKeys.Scopes.Chat));

        // Act
        await BuildEndpoint().RequestDelegate(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
        limiter.Verify(value => value.AcquireAsync(AICompletionQuotaFilter.PolicyName, "key:key-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Endpoint BuildEndpoint()
    {
        var services = new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider();
        var routes = new TestRouteBuilder(services);

        new AGUIEndpointStartup().Configure(null, routes, services);

        return Assert.Single(routes.DataSources.SelectMany(source => source.Endpoints));
    }

    private static DefaultHttpContext CreateContext(IAttemptLimiter limiter, params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddSingleton(limiter).BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, AIApiKeys.Scheme)),
        };

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/json";
        httpContext.Response.Body = new MemoryStream();

        return httpContext;
    }

    private sealed class TestRouteBuilder : IEndpointRouteBuilder
    {
        public TestRouteBuilder(IServiceProvider services)
            => ServiceProvider = services;

        public IServiceProvider ServiceProvider { get; }

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder()
            => new ApplicationBuilder(ServiceProvider);
    }
}

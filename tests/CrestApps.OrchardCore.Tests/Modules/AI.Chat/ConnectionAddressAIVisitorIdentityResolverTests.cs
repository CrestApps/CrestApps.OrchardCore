using System.Net;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A5 (c): CrestApps.Core's visitor resolver (pinned 2.0.0-preview.367) takes the first <c>X-Forwarded-For</c>
/// value of the raw request as the visitor's address, so a client picks a new address per request and escapes the
/// address-keyed chat limits.
/// </summary>
public sealed class ConnectionAddressAIVisitorIdentityResolverTests
{
    [Fact]
    public void Resolve_IgnoresAClientSetForwardedForHeader()
    {
        var resolver = CreateResolver(AIVisitorRemoteAddressMode.PlainText, "203.0.113.7", forwardedFor: "198.51.100.1");

        var identity = resolver.Resolve();

        Assert.Equal("203.0.113.7", identity.RemoteAddress);
    }

    [Fact]
    public void Resolve_GivesTheSameHashWhateverForwardedForSays()
    {
        var first = CreateResolver(AIVisitorRemoteAddressMode.Hashed, "203.0.113.7", forwardedFor: "198.51.100.1").Resolve();
        var second = CreateResolver(AIVisitorRemoteAddressMode.Hashed, "203.0.113.7", forwardedFor: "198.51.100.2").Resolve();

        Assert.NotNull(first.RemoteAddressHash);
        Assert.Equal(first.RemoteAddressHash, second.RemoteAddressHash);
    }

    /// <summary>
    /// The two chat features do not depend on each other, so either startup may run last. AI Chat Interactions calls the
    /// package's <c>AddCoreAIChatInteractions</c>, which registers the package resolver again; whichever runs last, the
    /// container must hand out the connection-address resolver.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheContainer_ResolvesTheConnectionAddressResolver_WithBothChatFeaturesOn(bool chatFirst)
    {
        var services = new ServiceCollection();
        Action<IServiceCollection> chat = s => new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(s);
        Action<IServiceCollection> interactions = s => new CrestApps.OrchardCore.AI.Chat.Interactions.Startup().ConfigureServices(s);

        (chatFirst ? chat : interactions)(services);
        (chatFirst ? interactions : chat)(services);

        var registered = services.Last(descriptor => descriptor.ServiceType == typeof(IAIVisitorIdentityResolver));

        Assert.Equal(typeof(ConnectionAddressAIVisitorIdentityResolver), registered.ImplementationType);
    }

    /// <summary>
    /// CrestApps.Core finding carried here: on the long-polling transport a hub call runs after the request that opened
    /// the connection ended, so the request accessor is empty and the session-start limiter got no key. The hub filter
    /// gives the resolver the address of SignalR's own copy of the connection's request.
    /// </summary>
    [Fact]
    public async Task Resolve_DuringAHubCallWithNoCurrentRequest_TakesTheConnectionsAddress()
    {
        // Arrange: the accessor is empty, as it is on long polling; the hub connection keeps its own request.
        var connectionRequest = new DefaultHttpContext();
        connectionRequest.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        var features = new Microsoft.AspNetCore.Http.Features.FeatureCollection();
        features.Set<Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature>(new HttpContextFeature { HttpContext = connectionRequest });
        var callerContext = Mock.Of<Microsoft.AspNetCore.SignalR.HubCallerContext>(context => context.Features == features);
        var invocation = new Microsoft.AspNetCore.SignalR.HubInvocationContext(callerContext, new ServiceCollection().BuildServiceProvider(), Mock.Of<Microsoft.AspNetCore.SignalR.Hub>(), typeof(object).GetMethod(nameof(ToString)), []);
        var resolver = new ConnectionAddressAIVisitorIdentityResolver(
            new FixedHttpContextAccessor(),
            Options.Create(new AIVisitorIdentityOptions { RemoteAddressMode = AIVisitorRemoteAddressMode.Hashed }),
            Mock.Of<IHostEnvironment>(env => env.ApplicationName == "Tests"),
            new EphemeralDataProtectionProvider());
        AIVisitorIdentity duringCall = null;

        // Act
        await new ChatHubConnectionAddressFilter().InvokeMethodAsync(invocation, _ =>
        {
            duringCall = resolver.Resolve();

            return ValueTask.FromResult<object>(null);
        });

        var outsideCall = resolver.Resolve();

        // Assert
        Assert.NotNull(duringCall.RemoteAddressHash);
        Assert.Equal(CreateResolver(AIVisitorRemoteAddressMode.Hashed, "203.0.113.7", forwardedFor: null).Resolve().RemoteAddressHash, duringCall.RemoteAddressHash);
        Assert.Null(outsideCall.RemoteAddressHash);
        Assert.Null(ChatHubConnectionAddressFilter.CurrentAddress);
    }

    private static ConnectionAddressAIVisitorIdentityResolver CreateResolver(AIVisitorRemoteAddressMode mode, string remoteAddress, string forwardedFor)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;

        var environment = Mock.Of<IHostEnvironment>(env => env.ApplicationName == "Tests");

        return new ConnectionAddressAIVisitorIdentityResolver(
            new FixedHttpContextAccessor { HttpContext = httpContext },
            Options.Create(new AIVisitorIdentityOptions { RemoteAddressMode = mode }),
            environment,
            new EphemeralDataProtectionProvider());
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    private sealed class HttpContextFeature : Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; }
    }
}

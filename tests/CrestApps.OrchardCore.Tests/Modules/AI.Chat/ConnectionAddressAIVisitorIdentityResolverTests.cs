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
}

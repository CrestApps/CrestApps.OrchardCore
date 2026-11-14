using System.Net;
using System.Text;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C6: the store reads a UCP platform's profile only for a caller it has authenticated (or a signed request from a
/// registered or allowed platform), reads at most the profile size limit even when the body is chunked, and keeps a
/// bounded number of profiles.
/// </summary>
public sealed class UcpProfileFetchTests
{
    private const string ProfileUrl = "https://agent.example/.well-known/ucp";

    [Fact]
    public async Task RequestFilter_UnauthenticatedCallerNamingAProfile_NeverFetchesIt()
    {
        // Arrange
        var handler = new ProfileHandler();
        var httpContext = CreateRequest(CreateServices(handler));

        // Act
        var result = await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task RequestFilter_SignedRequestFromAnUnknownPlatform_NeverFetchesItsProfile()
    {
        // Arrange
        var handler = new ProfileHandler();
        var httpContext = CreateRequest(CreateServices(handler));

        httpContext.Request.Headers["Signature-Input"] = "sig1=(\"@method\");keyid=\"k1\";alg=\"ecdsa-p256-sha256\"";
        httpContext.Request.Headers["Signature"] = "sig1=:AAAA:";

        // Act
        var result = await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task GetAsync_ChunkedProfileLargerThanTheLimit_IsRefusedAfterABoundedRead()
    {
        // Arrange
        var stream = new EndlessStream();
        var handler = new ProfileHandler(() => new StreamContent(stream));

        using var provider = CreateServices(handler);
        using var scope = provider.CreateScope();

        // Act
        var (profile, error) = await scope.ServiceProvider.GetRequiredService<UcpProfileClient>().GetAsync(ProfileUrl, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(profile);
        Assert.Equal("profile_malformed", error);
        Assert.True(stream.BytesProduced <= 512 * 1024, $"Read {stream.BytesProduced} bytes of a profile limited to 256 KB.");
    }

    [Fact]
    public async Task GetAsync_ManyDistinctProfiles_KeepsABoundedNumberCached()
    {
        // Arrange
        var handler = new ProfileHandler();

        using var provider = CreateServices(handler);

        // Act
        for (var i = 0; i <= 1000; i++)
        {
            using var scope = provider.CreateScope();

            await scope.ServiceProvider.GetRequiredService<UcpProfileClient>().GetAsync($"https://agent{i}.example/ucp", TestContext.Current.CancellationToken);
        }

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<UcpProfileClient>().GetAsync("https://agent0.example/ucp", TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(1002, handler.Requests);
    }

    [Fact]
    public async Task GetAsync_AProfileThatCantBeRead_IsNotFetchedAgainRightAway()
    {
        // Arrange: SEC-C6. Each failed read can cost a 10-second outbound call; a signed request from an unregistered
        // platform must not be able to make the store repeat it on every request.
        var handler = new ProfileHandler(() => new StringContent("{ \"not\": \"a profile\" }", Encoding.UTF8, "application/json"));

        using var provider = CreateServices(handler);

        // Act
        var errors = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            using var scope = provider.CreateScope();

            errors.Add((await scope.ServiceProvider.GetRequiredService<UcpProfileClient>().GetAsync(ProfileUrl, TestContext.Current.CancellationToken)).Error);
        }

        // Assert
        Assert.Equal(1, handler.Requests);
        Assert.All(errors, error => Assert.Equal("profile_malformed", error));
    }

    private static ServiceProvider CreateServices(ProfileHandler handler, AgenticCommerceSettings settings = null)
    {
        var services = new ServiceCollection();
        var factory = new Mock<IHttpClientFactory>();
        var platforms = new Mock<IAgentPlatformStore>();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();

        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        site.Put(settings ?? new AgenticCommerceSettings());
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);

        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton(factory.Object);
        services.AddSingleton(platforms.Object);
        services.AddSingleton(siteService.Object);
        services.AddSingleton(Mock.Of<global::OrchardCore.Modules.IClock>(c => c.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<UcpProfileCache>();
        services.AddScoped<UcpProfileClient>();
        services.AddScoped(sp => new AgentPlatformAuthenticator(platforms.Object, NullLogger<AgentPlatformAuthenticator>.Instance));

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateRequest(IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
        };
        var body = "{}"u8.ToArray();

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("shop.example");
        httpContext.Request.Path = "/ucp/checkout-sessions";
        httpContext.Request.Body = new MemoryStream(body);
        httpContext.Request.ContentLength = body.Length;
        httpContext.Request.Headers[UcpConstants.AgentHeader] = $"profile=\"{ProfileUrl}\"";

        return httpContext;
    }

    private sealed class ProfileHandler : HttpMessageHandler
    {
        private readonly Func<HttpContent> _content;
        private int _requests;

        public ProfileHandler(Func<HttpContent> content = null)
        {
            _content = content ?? (() => new StringContent("""{ "ucp": { "version": "2026-08-25" } }""", Encoding.UTF8, "application/json"));
        }

        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = _content(),
            });
        }
    }
}

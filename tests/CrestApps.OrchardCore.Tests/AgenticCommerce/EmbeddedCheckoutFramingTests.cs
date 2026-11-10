using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Handlers;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// UCP embedded checkout lets only the origins a platform lists frame its checkout: the origins are checked and
/// normalized, the response's policy gets a <c>frame-ancestors</c> directive that names them (replacing the site's own,
/// keeping the rest), the anti-framing header goes, and the cookies move to the platform's partition. Every other
/// response is left alone.
/// </summary>
public sealed class EmbeddedCheckoutFramingTests
{
    [Theory]
    [InlineData("https://agent.example", "https://agent.example")]
    [InlineData("https://Agent.Example/", "https://agent.example")]
    [InlineData("  https://agent.example:8443  ", "https://agent.example:8443")]
    [InlineData("https://agent.example:443", "https://agent.example")]
    [InlineData("http://localhost:5173", "http://localhost:5173")]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080")]
    public void NormalizeOrigin_ReturnsTheOriginInTheShapeBrowsersReportIt(string value, string expected)
        => Assert.Equal(expected, EmbeddingPolicy.NormalizeOrigin(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("agent.example")]
    [InlineData("http://agent.example")]
    [InlineData("https://agent.example/checkout")]
    [InlineData("https://agent.example/?a=1")]
    [InlineData("https://agent.example/#top")]
    [InlineData("https://user:pass@agent.example")]
    [InlineData("https://*.agent.example")]
    [InlineData("ftp://agent.example")]
    [InlineData("javascript:alert(1)")]
    public void NormalizeOrigin_RefusesWhatIsNotAnOriginThatMayFrameAPage(string value)
        => Assert.Null(EmbeddingPolicy.NormalizeOrigin(value));

    [Fact]
    public void NormalizeOrigins_KeepsValidOriginsOnceInTheirOrder()
    {
        var origins = EmbeddingPolicy.NormalizeOrigins(["https://b.example", "nope", "https://A.example/", "https://b.example/"]);

        Assert.Equal(new[] { "https://b.example", "https://a.example" }, origins);
    }

    [Fact]
    public void IsAllowedOrigin_MatchesOnlyAListedOrigin()
    {
        string[] allowed = ["https://agent.example"];

        Assert.True(EmbeddingPolicy.IsAllowedOrigin("https://agent.example", allowed));
        Assert.False(EmbeddingPolicy.IsAllowedOrigin("https://agent.example.evil", allowed));
        Assert.False(EmbeddingPolicy.IsAllowedOrigin("null", allowed));
    }

    [Fact]
    public void BuildFrameAncestors_NamesTheSources_OrNoneWhenThereAreNone()
    {
        Assert.Equal("frame-ancestors 'self' https://agent.example", EmbeddingPolicy.BuildFrameAncestors(["'self'", "https://agent.example", "https://agent.example"]));
        Assert.Equal("frame-ancestors 'none'", EmbeddingPolicy.BuildFrameAncestors([]));
    }

    [Theory]
    [InlineData(null, "frame-ancestors https://agent.example")]
    [InlineData("frame-ancestors 'self'", "frame-ancestors https://agent.example")]
    [InlineData("default-src 'self';frame-ancestors 'none';script-src 'self'", "default-src 'self'; script-src 'self'; frame-ancestors https://agent.example")]
    [InlineData("default-src 'self'; frame-ancestors-extra x", "default-src 'self'; frame-ancestors-extra x; frame-ancestors https://agent.example")]
    public void MergeFrameAncestors_ReplacesOnlyTheFrameAncestorsDirective(string policy, string expected)
        => Assert.Equal(expected, EmbeddingPolicy.MergeFrameAncestors(policy, ["https://agent.example"]));

    [Theory]
    [InlineData("a=1; path=/; samesite=lax; httponly", "a=1; path=/; httponly; secure; samesite=none; partitioned")]
    [InlineData("a=1; path=/; secure; samesite=strict; httponly", "a=1; path=/; httponly; secure; samesite=none; partitioned")]
    [InlineData("a=1; secure; samesite=none; partitioned", "a=1; secure; samesite=none; partitioned")]
    public void PartitionCookie_KeepsTheCookieForTheFramingSiteOnly(string setCookie, string expected)
        => Assert.Equal(expected, EmbeddingPolicy.PartitionCookie(setCookie));

    [Theory]
    [InlineData("/shop/Checkout/s1", "/shop/Checkout/s1", true)]
    [InlineData("/shop/checkout/s1/payment/begin", "/shop/Checkout/s1", true)]
    [InlineData("/shop/Checkout/s10", "/shop/Checkout/s1", false)]
    [InlineData("/shop/Checkout", "/shop/Checkout/s1", false)]
    [InlineData("/shop/Checkout/s1", null, false)]
    public void IsWithin_MatchesThePathAndWhatIsBelowIt(string path, string prefix, bool expected)
        => Assert.Equal(expected, EmbeddingPolicy.IsWithin(path, prefix));

    [Fact]
    public async Task Apply_LetsTheSourcesFrameTheResponse_AndPartitionsItsCookies()
    {
        // Arrange
        var (context, response) = CreateContext();
        context.Response.Headers[HeaderNames.XFrameOptions] = "SAMEORIGIN";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'self'";
        context.Response.Headers.SetCookie = "guest=abc; path=/; samesite=lax; httponly";

        // Act
        EmbeddingPolicy.Apply(context.Response, ["https://agent.example"], partitionCookies: true);
        await response.StartAsync();

        // Assert
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.XFrameOptions));
        Assert.Equal("default-src 'self'; frame-ancestors https://agent.example", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Equal("guest=abc; path=/; httponly; secure; samesite=none; partitioned", context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Apply_WithoutSources_KeepsTheAntiFramingHeaders()
    {
        // Arrange
        var (context, response) = CreateContext();
        context.Response.Headers[HeaderNames.XFrameOptions] = "SAMEORIGIN";
        context.Response.Headers.SetCookie = "a=1; samesite=lax";

        // Act
        EmbeddingPolicy.Apply(context.Response, [], partitionCookies: false);
        await response.StartAsync();

        // Assert
        Assert.Equal("SAMEORIGIN", context.Response.Headers[HeaderNames.XFrameOptions].ToString());
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentSecurityPolicy));
        Assert.Equal("a=1; samesite=lax", context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void NormalizeOrigins_OnThePlatform_KeepsAnInvalidEntrySoValidationNamesIt()
    {
        var origins = AgentPlatformHandler.NormalizeOrigins(["https://Agent.Example/", "agent.example"]);

        Assert.Equal(new[] { "https://agent.example", "agent.example" }, origins);
    }

    [Fact]
    public void EmbeddedCheckoutContext_IsAuthorizedOnlyWhileTheHostsCredentialHolds()
    {
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(new EmbeddedCheckoutContext().IsAuthorized(now));
        Assert.False(new EmbeddedCheckoutContext { AuthRequired = true }.IsAuthorized(now));
        Assert.True(new EmbeddedCheckoutContext { AuthRequired = true, AuthenticatedUntilUtc = now.AddMinutes(5) }.IsAuthorized(now));
        Assert.False(new EmbeddedCheckoutContext { AuthRequired = true, AuthenticatedUntilUtc = now.AddMinutes(-1) }.IsAuthorized(now));
    }

    [Fact]
    public void AddBinding_DeclaresTheEmbeddedTransportWithNoDelegation()
    {
        // Arrange
        var ucp = new JsonObject
        {
            ["version"] = "2026-08-25",
            ["capabilities"] = new JsonObject(),
        };

        // Act
        UcpEmbeddedCheckoutExtension.AddBinding(ucp, "2026-08-25");

        // Assert
        var binding = Assert.Single(ucp["services"]!["dev.ucp.shopping"]!.AsArray())!.AsObject();

        Assert.Equal("2026-08-25", binding["version"]!.GetValue<string>());
        Assert.Equal("embedded", binding["transport"]!.GetValue<string>());
        Assert.Empty(binding["config"]!["delegate"]!.AsArray());
    }

    [Fact]
    public void AgentPlatform_ExportsItsEmbeddingOrigins()
    {
        var platform = new AgentPlatform { EmbeddedCheckoutOrigins = ["https://agent.example"] };

        var json = System.Text.Json.JsonSerializer.SerializeToNode(platform)!.AsObject();

        Assert.Equal("https://agent.example", json[nameof(AgentPlatform.EmbeddedCheckoutOrigins)]![0]!.GetValue<string>());
    }

    private static (DefaultHttpContext Context, StartableResponseFeature Response) CreateContext()
    {
        var context = new DefaultHttpContext();
        var response = new StartableResponseFeature(context.Features.Get<IHttpResponseFeature>()!);

        context.Features.Set<IHttpResponseFeature>(response);

        return (context, response);
    }

    // A response feature that runs the callbacks registered to run when the response starts.
    private sealed class StartableResponseFeature : IHttpResponseFeature
    {
        private readonly IHttpResponseFeature _inner;
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public StartableResponseFeature(IHttpResponseFeature inner)
        {
            _inner = inner;
        }

        public int StatusCode { get => _inner.StatusCode; set => _inner.StatusCode = value; }

        public string ReasonPhrase { get => _inner.ReasonPhrase; set => _inner.ReasonPhrase = value; }

        public IHeaderDictionary Headers { get => _inner.Headers; set => _inner.Headers = value; }

        [Obsolete("Use IHttpResponseBodyFeature.Stream instead.")]
        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state)
            => _starting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task StartAsync()
        {
            for (var i = _starting.Count - 1; i >= 0; i--)
            {
                await _starting[i].Callback(_starting[i].State);
            }

            HasStarted = true;
        }
    }
}

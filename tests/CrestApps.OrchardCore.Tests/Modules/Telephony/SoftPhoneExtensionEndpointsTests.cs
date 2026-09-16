using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class SoftPhoneExtensionEndpointsTests
{
    [Fact]
    public async Task ExtensionConfig_WhenUnauthorized_ReturnsForbid()
    {
        // Arrange
        var httpContext = CreateHttpContext(withUserId: true);

        // Act
        var result = await SoftPhoneExtensionEndpoints.HandleExtensionConfigAsync(
            new TestAuthorizationService(isAuthorized: false),
            new FakeLinkGenerator("/prefix/contact-center/agent/current-incoming-offer"),
            httpContext);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task ExtensionConfig_WhenAuthenticatedButHasNoUserId_ReturnsForbid()
    {
        // Arrange
        var httpContext = CreateHttpContext(withUserId: false);

        // Act
        var result = await SoftPhoneExtensionEndpoints.HandleExtensionConfigAsync(
            new TestAuthorizationService(isAuthorized: true),
            new FakeLinkGenerator("/prefix/contact-center/agent/current-incoming-offer"),
            httpContext);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task ExtensionConfig_WhenAuthorized_ReturnsAbsoluteHubAndPageUrls()
    {
        // Arrange
        var httpContext = CreateHttpContext(withUserId: true);
        var offerUrl = "https://tenant.example/prefix/contact-center/agent/current-incoming-offer";

        // Act
        var result = await SoftPhoneExtensionEndpoints.HandleExtensionConfigAsync(
            new TestAuthorizationService(isAuthorized: true),
            new FakeLinkGenerator(offerUrl),
            httpContext);

        // Assert - the config carries the origin-absolute hub and page URLs the extension needs (it runs in its
        // own window/background, so a relative URL would not resolve), and the offer URL the link generator gave.
        var json = SerializeValue(result);
        Assert.Contains("\"hubUrl\":\"https://tenant.example/prefix/", json, StringComparison.Ordinal);
        Assert.Contains("\"softPhoneUrl\":\"https://tenant.example/prefix/softphone\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"currentIncomingOfferUrl\":\"{offerUrl}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"userId\":\"user-1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtensionConfig_WhenContactCenterRouteIsAbsent_OmitsTheOfferUrl()
    {
        // Arrange - Contact Center is not enabled, so the pending-offer route does not resolve and the link
        // generator returns null. The extension must still get a config for outbound/browser-originated calls.
        var httpContext = CreateHttpContext(withUserId: true);

        // Act
        var result = await SoftPhoneExtensionEndpoints.HandleExtensionConfigAsync(
            new TestAuthorizationService(isAuthorized: true),
            new FakeLinkGenerator(uri: null),
            httpContext);

        // Assert
        var json = SerializeValue(result);
        Assert.Contains("\"currentIncomingOfferUrl\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"softPhoneUrl\":\"https://tenant.example/prefix/softphone\"", json, StringComparison.Ordinal);
    }

    private static readonly JsonSerializerOptions _camelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string SerializeValue(IResult result)
    {
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;

        return JsonSerializer.Serialize(value, _camelCase);
    }

    private static DefaultHttpContext CreateHttpContext(bool withUserId)
    {
        var claims = new List<Claim>();

        if (withUserId)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        }

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };

        context.Request.Scheme = "https";
        context.Request.Host = new HostString("tenant.example");
        context.Request.PathBase = "/prefix";

        return context;
    }

    private sealed class TestAuthorizationService : IAuthorizationService
    {
        private readonly bool _isAuthorized;

        public TestAuthorizationService(bool isAuthorized)
        {
            _isAuthorized = isAuthorized;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }
    }

    // Returns a fixed URI for GetUriByName (used to stand in for the Contact Center offer route), or null to
    // simulate the route being absent when Contact Center is not enabled.
    private sealed class FakeLinkGenerator : LinkGenerator
    {
        private readonly string _uri;

        public FakeLinkGenerator(string uri)
        {
            _uri = uri;
        }

        public override string GetPathByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary ambientValues = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions options = null)
            => _uri;

        public override string GetPathByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions options = null)
            => _uri;

        public override string GetUriByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary ambientValues = null,
            string scheme = null,
            HostString? host = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions options = null)
            => _uri;

        public override string GetUriByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            string scheme,
            HostString host,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions options = null)
            => _uri;
    }
}

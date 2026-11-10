using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// A platform's UCP profile agrees a version the store serves and the capabilities both sides offer in it; an
/// extension whose parent isn't agreed is dropped, and 2026-04-08 names the AP2 extension the old way.
/// </summary>
public sealed class UcpNegotiatorTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NegotiateAsync_ForAVersionTheStoreDoesNotServe_AgreesNothing()
    {
        // Arrange
        var negotiator = CreateNegotiator();
        var profile = Profile("2025-01-01", ("dev.ucp.shopping.checkout", "2025-01-01"));

        // Act
        var negotiation = await negotiator.NegotiateAsync(profile);

        // Assert
        Assert.Null(negotiation.Version);
        Assert.Empty(negotiation.Capabilities);
    }

    [Fact]
    public async Task NegotiateAsync_WithoutAProfile_AgreesTheVersionTheHeaderAsksFor()
    {
        var negotiation = await CreateNegotiator().NegotiateAsync(platformProfile: null, AgenticCommerceConstants.Versions.Ucp20260408);

        Assert.Equal(AgenticCommerceConstants.Versions.Ucp20260408, negotiation.Version);
        Assert.Contains(UcpConstants.Capabilities.Checkout, negotiation.Capabilities.Keys);
    }

    [Fact]
    public async Task NegotiateAsync_WithoutAProfile_RefusesAVersionTheStoreDoesNotServe()
    {
        var negotiation = await CreateNegotiator().NegotiateAsync(platformProfile: null, "2099-01-01");

        Assert.Null(negotiation.Version);
    }

    [Fact]
    public async Task NegotiateAsync_WithoutAPlatformProfile_AgreesTheNewestVersionWithEveryCapabilityButAp2()
    {
        // Arrange
        var negotiator = CreateNegotiator();

        // Act
        var negotiation = await negotiator.NegotiateAsync(platformProfile: null);

        // Assert
        Assert.Equal(AgenticCommerceConstants.Versions.Ucp20260825, negotiation.Version);
        Assert.Equal(
            new[]
            {
                UcpConstants.Capabilities.Checkout,
                UcpConstants.Capabilities.Fulfillment,
                UcpConstants.Capabilities.Discount,
                UcpConstants.Capabilities.BuyerConsent,
                UcpConstants.Capabilities.Order,
            }.Order(StringComparer.Ordinal),
            negotiation.Capabilities.Keys.Order(StringComparer.Ordinal));
        Assert.All(negotiation.Capabilities.Values, version => Assert.Equal(AgenticCommerceConstants.Versions.Ucp20260825, version));
    }

    [Fact]
    public async Task NegotiateAsync_KeepsOnlyTheCapabilitiesOfferedInTheSameVersion()
    {
        // Arrange
        var negotiator = CreateNegotiator();
        var version = AgenticCommerceConstants.Versions.Ucp20260825;
        var profile = Profile(
            version,
            (UcpConstants.Capabilities.Checkout, version),
            (UcpConstants.Capabilities.Discount, version),
            (UcpConstants.Capabilities.Fulfillment, AgenticCommerceConstants.Versions.Ucp20260408),
            ("dev.example.unknown", version));

        // Act
        var negotiation = await negotiator.NegotiateAsync(profile);

        // Assert
        Assert.Equal(version, negotiation.Version);
        Assert.Equal(
            new[] { UcpConstants.Capabilities.Checkout, UcpConstants.Capabilities.Discount }.Order(StringComparer.Ordinal),
            negotiation.Capabilities.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NegotiateAsync_DropsAnExtensionWhoseParentIsNotAgreed()
    {
        // Arrange
        var negotiator = CreateNegotiator();
        var version = AgenticCommerceConstants.Versions.Ucp20260825;
        var profile = Profile(
            version,
            (UcpConstants.Capabilities.Fulfillment, version),
            (UcpConstants.Capabilities.Discount, version),
            (UcpConstants.Capabilities.Ap2Mandate, version),
            (UcpConstants.Capabilities.Order, version));

        // Act
        var negotiation = await negotiator.NegotiateAsync(profile);

        // Assert
        var capability = Assert.Single(negotiation.Capabilities);

        Assert.Equal(UcpConstants.Capabilities.Order, capability.Key);
        Assert.False(negotiation.Extends.ContainsKey(UcpConstants.Capabilities.Fulfillment));
    }

    [Fact]
    public async Task NegotiateAsync_For20260408_UsesTheLegacyAp2Name()
    {
        // Arrange
        var negotiator = CreateNegotiator();
        var version = AgenticCommerceConstants.Versions.Ucp20260408;
        var profile = Profile(
            version,
            (UcpConstants.Capabilities.Checkout, version),
            (UcpConstants.Capabilities.Ap2MandateLegacy, version),
            (UcpConstants.Capabilities.Ap2Mandate, version));

        // Act
        var negotiation = await negotiator.NegotiateAsync(profile);

        // Assert
        Assert.Equal(version, negotiation.Version);
        Assert.Equal(version, negotiation.Capabilities[UcpConstants.Capabilities.Ap2MandateLegacy]);
        Assert.False(negotiation.Capabilities.ContainsKey(UcpConstants.Capabilities.Ap2Mandate));
        Assert.Equal([UcpConstants.Capabilities.Checkout], negotiation.Extends[UcpConstants.Capabilities.Ap2MandateLegacy]);
    }

    [Fact]
    public async Task GetOfferAsync_PointsTheRestServiceAtTheRequestsBaseUrl()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var offer = await builder.GetOfferAsync(AgenticCommerceConstants.Versions.Ucp20260825);

        // Assert
        var service = Assert.Single(offer.Services);

        Assert.Equal("https://store.example.com", offer.BaseUrl);
        Assert.Equal("https://store.example.com/ucp/v1", service.Endpoint);
    }

    private static UcpNegotiator CreateNegotiator()
        => new(CreateBuilder());

    private static UcpProfileBuilder CreateBuilder()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("store.example.com");

        var urls = new AgentSiteUrls(
            new HttpContextAccessor { HttpContext = httpContext },
            Mock.Of<ISiteService>(),
            new SignedTokenService(new EphemeralDataProtectionProvider(), new TestClock(_now)));

        return new UcpProfileBuilder(
            [new UcpShoppingProfileContributor(), new Ap2ProfileContributor()],
            Mock.Of<IAgentCheckoutService>(),
            Mock.Of<IMerchantSigningKeyService>(),
            urls);
    }

    private static JsonObject Profile(string version, params (string Name, string Version)[] capabilities)
    {
        var offered = new JsonObject();

        foreach (var (name, capabilityVersion) in capabilities)
        {
            offered[name] = new JsonArray(new JsonObject { ["version"] = capabilityVersion });
        }

        return new JsonObject
        {
            ["ucp"] = new JsonObject
            {
                ["version"] = version,
                ["capabilities"] = offered,
            },
        };
    }
}

using System.Net;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// A platform may call only while it is enabled, in a protocol it uses, and from an address its allow list names:
/// a single address, a CIDR range, or none at all to allow every address.
/// </summary>
public sealed class AgentPlatformAuthenticatorTests
{
    [Theory]
    [InlineData(new[] { "203.0.113.5" }, "203.0.113.5", true)]
    [InlineData(new[] { "203.0.113.5" }, "203.0.113.6", false)]
    [InlineData(new[] { "203.0.113.0/24" }, "203.0.113.77", true)]
    [InlineData(new[] { "203.0.113.0/24" }, "203.0.114.1", false)]
    [InlineData(new[] { "203.0.113.5" }, "::ffff:203.0.113.5", true)]
    [InlineData(new[] { "203.0.113.0/24" }, "::ffff:203.0.113.9", true)]
    [InlineData(new[] { "2001:db8::/32" }, "2001:db8::1", true)]
    [InlineData(new[] { "not an address", "203.0.113.5" }, "203.0.113.5", true)]
    public void IsAllowed_MatchesTheAllowList(string[] ranges, string address, bool expected)
    {
        // Arrange
        var platform = new AgentPlatform { AllowedIpRanges = [.. ranges] };

        // Act
        var allowed = AgentPlatformAuthenticator.IsAllowed(platform, IPAddress.Parse(address));

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void IsAllowed_WithAnEmptyAllowList_AllowsEveryAddress()
    {
        // Arrange
        var platform = new AgentPlatform();

        // Act
        var allowed = AgentPlatformAuthenticator.IsAllowed(platform, IPAddress.Parse("198.51.100.1"));

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public void IsAllowed_WithAnAllowListAndNoAddress_Refuses()
    {
        // Arrange
        var platform = new AgentPlatform { AllowedIpRanges = ["203.0.113.5"] };

        // Act
        var allowed = AgentPlatformAuthenticator.IsAllowed(platform, address: null);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void Check_ForADisabledPlatform_IsForbidden()
    {
        // Arrange
        var platform = new AgentPlatform { Enabled = false, Protocols = [AgenticCommerceConstants.Protocols.Ucp] };

        // Act
        var result = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, Request("203.0.113.5"));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void Check_ForAProtocolThePlatformDoesNotUse_IsForbidden()
    {
        // Arrange
        var platform = new AgentPlatform { Protocols = [AgenticCommerceConstants.Protocols.Acp] };

        // Act
        var result = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, Request("203.0.113.5"));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Contains("UCP", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_FromAnAddressOutsideTheAllowList_IsForbidden()
    {
        // Arrange
        var platform = new AgentPlatform { Protocols = [AgenticCommerceConstants.Protocols.Ucp], AllowedIpRanges = ["203.0.113.0/24"] };

        // Act
        var result = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, Request("198.51.100.1"));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void Check_ForAnAllowedPlatform_SignsItInWithTheLinkedBuyer()
    {
        // Arrange
        var platform = new AgentPlatform { Protocols = ["UCP"], AllowedIpRanges = ["203.0.113.0/24"] };

        // Act
        var result = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, Request("203.0.113.5"), "user-1");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Same(platform, result.Platform);
        Assert.Equal("user-1", result.UserId);
    }

    private static DefaultHttpContext Request(string address)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return httpContext;
    }
}

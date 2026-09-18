using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;
using CrestApps.OrchardCore.Telephony.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Tests.SignalR;

public sealed class SignalRHubRoutesTests
{
    /// <summary>
    /// The paths the browser clients connect to, pinned against the concrete hub types.
    /// </summary>
    /// <remarks>
    /// A path is derived from the hub's type name alone, so anything that renames a hub - or replaces it with a
    /// differently named one - silently moves the endpoint every agent browser is already pointed at. Nothing
    /// else in the suite asserts these strings.
    /// </remarks>
    public static TheoryData<string, string> HubPaths => new()
    {
        { "/Communication/Hub/ContactCenterHub", SignalRHubRoutes.GetHubPath<ContactCenterHub>() },
        { "/Communication/Hub/TelephonyHub", SignalRHubRoutes.GetHubPath<TelephonyHub>() },
        { "/Communication/Hub/SmsPortalHub", SignalRHubRoutes.GetHubPath<SmsPortalHub>() },
    };

    [Theory]
    [MemberData(nameof(HubPaths))]
    public void GetHubPath_ForASuiteHub_IsTheAgreedPath(string expected, string actual)
        => Assert.Equal(expected, actual);

    [Fact]
    public void GetHubPath_ForAHubWithABaseClass_StillUsesTheConcreteName()
    {
        // The suite's hubs are sealed subclasses of a host-neutral base. A path derived from anything but the
        // concrete name - the base, the namespace, the assembly - would move every browser's endpoint.
        Assert.Equal("/Communication/Hub/DerivedTestHub", SignalRHubRoutes.GetHubPath<DerivedTestHub>());
    }

    [Fact]
    public void GetHubPath_ForHub_ReturnsConventionalRoute()
    {
        // Act
        var path = SignalRHubRoutes.GetHubPath<TestHub>();

        // Assert
        Assert.Equal("/Communication/Hub/TestHub", path);
    }

    [Fact]
    public void GetTenantAwareHubUrl_WithoutPathBase_ReturnsHubRoute()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var url = SignalRHubRoutes.GetTenantAwareHubUrl<TestHub>(httpContext);

        // Assert
        Assert.Equal("/Communication/Hub/TestHub", url);
    }

    [Fact]
    public void GetTenantAwareHubUrl_WithTenantPathBase_ReturnsPrefixedHubRoute()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/tenant-a";

        // Act
        var url = SignalRHubRoutes.GetTenantAwareHubUrl<TestHub>(httpContext);

        // Assert
        Assert.Equal("/tenant-a/Communication/Hub/TestHub", url);
    }

    [Fact]
    public void GetTenantAwareHubUrl_WhenHttpContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SignalRHubRoutes.GetTenantAwareHubUrl<TestHub>(null!));
    }

    private class TestHub : Hub
    {
    }

    private sealed class DerivedTestHub : TestHub
    {
    }
}

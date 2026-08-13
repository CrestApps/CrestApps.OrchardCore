using CrestApps.OrchardCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Tests.SignalR;

public sealed class SignalRHubRoutesTests
{
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

    private sealed class TestHub : Hub
    {
    }
}

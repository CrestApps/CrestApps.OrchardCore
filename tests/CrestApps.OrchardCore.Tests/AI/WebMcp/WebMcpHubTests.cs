using System.Security.Claims;
using CrestApps.OrchardCore.AI.WebMcp.Hubs;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpHubTests
{
    [Fact]
    public async Task RegisterPageTools_RecordsTheValidToolsOfTheUsersTab()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var hub = CreateHub(registry, Mock.Of<IWebMcpBrowserToolInvoker>());

        // Act
        var count = await hub.RegisterPageTools(
        [
            Descriptor("page_get_info"),
            Descriptor("not valid"),
        ]);

        // Assert
        Assert.Equal(1, count);
        Assert.True(registry.TryGet("user-1", out var registration));
        Assert.Equal("connection-1", registration.ConnectionId);
        Assert.Equal(["page_get_info"], registration.Tools.Select(tool => tool.Name));
    }

    [Fact]
    public async Task RegisterPageTools_ForgetsTheTabsToolsWhenNoneIsValid()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var hub = CreateHub(registry, Mock.Of<IWebMcpBrowserToolInvoker>());

        await hub.RegisterPageTools([Descriptor("page_get_info")]);

        // Act
        var count = await hub.RegisterPageTools([Descriptor("not valid")]);

        // Assert
        Assert.Equal(0, count);
        Assert.False(registry.TryGet("user-1", out _));
    }

    [Fact]
    public async Task RegisterPageTools_RefusesAUserWithoutThePermission()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var hub = CreateHub(registry, Mock.Of<IWebMcpBrowserToolInvoker>(), allowed: false);

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() => hub.RegisterPageTools([Descriptor("page_get_info")]));
        Assert.False(registry.TryGet("user-1", out _));
    }

    [Fact]
    public async Task RegisterPageTools_RefusesAConnectionWithoutAUser()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var hub = CreateHub(registry, Mock.Of<IWebMcpBrowserToolInvoker>(), userId: null);

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() => hub.RegisterPageTools([Descriptor("page_get_info")]));
    }

    [Fact]
    public async Task ClearPageTools_ForgetsTheTabsTools()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var hub = CreateHub(registry, Mock.Of<IWebMcpBrowserToolInvoker>());

        await hub.RegisterPageTools([Descriptor("page_get_info")]);

        // Act
        hub.ClearPageTools();

        // Assert
        Assert.False(registry.TryGet("user-1", out _));
    }

    [Fact]
    public void SubmitToolResult_CompletesTheCallForThisConnection()
    {
        // Arrange
        var invoker = new Mock<IWebMcpBrowserToolInvoker>();

        invoker
            .Setup(i => i.TryComplete("call-1", "connection-1", "Done.", false))
            .Returns(true);

        var hub = CreateHub(new DefaultWebMcpBrowserToolRegistry(), invoker.Object);

        // Act
        var completed = hub.SubmitToolResult("call-1", "Done.", false);

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ForgetsTheTabAndEndsItsCalls()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();
        var invoker = new Mock<IWebMcpBrowserToolInvoker>();
        var hub = CreateHub(registry, invoker.Object);

        await hub.RegisterPageTools([Descriptor("page_get_info")]);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        Assert.False(registry.TryGet("user-1", out _));
        invoker.Verify(i => i.CancelConnection("connection-1"), Times.Once());
    }

    private static WebMcpBrowserToolDescriptor Descriptor(string name)
    {
        return new WebMcpBrowserToolDescriptor
        {
            Name = name,
            Description = "A page tool.",
            InputSchema = """{"type":"object"}""",
        };
    }

    private static WebMcpHub CreateHub(
        IWebMcpBrowserToolRegistry registry,
        IWebMcpBrowserToolInvoker invoker,
        bool allowed = true,
        string userId = "user-1")
    {
        var claims = userId is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var context = new Mock<HubCallerContext>();

        context.SetupGet(c => c.ConnectionId).Returns("connection-1");
        context.SetupGet(c => c.User).Returns(user);

        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return new WebMcpHub(registry, invoker, authorizationService.Object)
        {
            Context = context.Object,
        };
    }
}

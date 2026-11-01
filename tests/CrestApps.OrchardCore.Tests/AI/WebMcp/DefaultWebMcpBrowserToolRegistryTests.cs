using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class DefaultWebMcpBrowserToolRegistryTests
{
    private static readonly IReadOnlyList<WebMcpBrowserTool> _tools =
    [
        new WebMcpBrowserTool
        {
            Name = "page_get_info",
            Description = "Describes the page.",
        },
    ];

    [Fact]
    public void TryGet_FindsWhatAUserRegistered()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();

        // Act
        registry.Register("user-1", "connection-1", _tools);

        // Assert
        Assert.True(registry.TryGet("user-1", out var registration));
        Assert.Equal("user-1", registration.UserId);
        Assert.Equal("connection-1", registration.ConnectionId);
        Assert.Same(_tools, registration.Tools);
        Assert.False(registry.TryGet("user-2", out _));
    }

    [Fact]
    public void Register_KeepsTheTabThatRegisteredLast()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();

        // Act
        registry.Register("user-1", "connection-1", _tools);
        registry.Register("user-1", "connection-2", _tools);

        // Assert
        Assert.True(registry.TryGet("user-1", out var registration));
        Assert.Equal("connection-2", registration.ConnectionId);
    }

    [Fact]
    public void Remove_OnlyRemovesTheEntryOfThatConnection()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();

        registry.Register("user-1", "connection-2", _tools);

        // Act
        var removedOther = registry.Remove("user-1", "connection-1");
        var removedOwn = registry.Remove("user-1", "connection-2");

        // Assert
        Assert.False(removedOther);
        Assert.True(removedOwn);
        Assert.False(registry.TryGet("user-1", out _));
    }

    [Fact]
    public void RemoveConnection_ForgetsEveryEntryOfAClosedConnection()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();

        registry.Register("user-1", "connection-1", _tools);
        registry.Register("user-2", "connection-2", _tools);

        // Act
        registry.RemoveConnection("connection-1");

        // Assert
        Assert.False(registry.TryGet("user-1", out _));
        Assert.True(registry.TryGet("user-2", out _));
    }

    [Fact]
    public void TryGet_FindsNothingForAnEmptyUser()
    {
        // Arrange
        var registry = new DefaultWebMcpBrowserToolRegistry();

        // Act & Assert
        Assert.False(registry.TryGet(null, out _));
        Assert.False(registry.TryGet(string.Empty, out _));
    }
}

using CrestApps.OrchardCore.AI.WebMcp;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpClientConfigurationHandlerTests
{
    [Fact]
    public async Task PageToolsHandler_AddsThePageToolsScriptWhenPageToolsAreOn()
    {
        // Arrange
        var context = new WebMcpClientConfigurationContext(new DefaultHttpContext(), isAdminPage: true);
        var handler = new WebMcpPageToolsClientConfigurationHandler(Options(new WebMcpPageOptions()));

        // Act
        await handler.ConfigureAsync(context);

        // Assert
        Assert.Equal([WebMcpConstants.ResourceNames.PageToolsScript], context.Scripts);
    }

    [Fact]
    public async Task PageToolsHandler_AddsNothingWhenPageToolsAreOff()
    {
        // Arrange
        var context = new WebMcpClientConfigurationContext(new DefaultHttpContext(), isAdminPage: true);
        var handler = new WebMcpPageToolsClientConfigurationHandler(Options(new WebMcpPageOptions
        {
            EnablePageTools = false,
        }));

        // Act
        await handler.ConfigureAsync(context);

        // Assert
        Assert.Empty(context.Scripts);
    }

    private static IOptionsSnapshot<WebMcpPageOptions> Options(WebMcpPageOptions options)
    {
        var snapshot = new Mock<IOptionsSnapshot<WebMcpPageOptions>>();

        snapshot
            .Setup(s => s.Value)
            .Returns(options);

        return snapshot.Object;
    }

    [Fact]
    public void AddScript_KeepsTheOrderAndAddsAScriptOnce()
    {
        // Arrange
        var context = new WebMcpClientConfigurationContext(new DefaultHttpContext(), isAdminPage: true);

        // Act
        context.AddScript("one");
        context.AddScript("two");
        context.AddScript("one");

        // Assert
        Assert.Equal(["one", "two"], context.Scripts);
    }
}

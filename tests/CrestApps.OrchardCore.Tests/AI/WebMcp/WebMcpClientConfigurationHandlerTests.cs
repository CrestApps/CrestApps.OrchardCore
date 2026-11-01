using CrestApps.OrchardCore.AI.WebMcp;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpClientConfigurationHandlerTests
{
    [Fact]
    public async Task PageToolsHandler_AddsThePageToolsScript()
    {
        // Arrange
        var context = new WebMcpClientConfigurationContext(new DefaultHttpContext(), isAdminPage: true);
        var handler = new WebMcpPageToolsClientConfigurationHandler();

        // Act
        await handler.ConfigureAsync(context);

        // Assert
        Assert.Equal([WebMcpConstants.ResourceNames.PageToolsScript], context.Scripts);
    }

    [Fact]
    public async Task ChatHandler_AddsTheBridgeScriptAndTheTenantHubAddress()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        httpContext.Request.PathBase = "/tenant";

        var context = new WebMcpClientConfigurationContext(httpContext, isAdminPage: false);
        var handler = new WebMcpChatClientConfigurationHandler();

        // Act
        await handler.ConfigureAsync(context);

        // Assert
        Assert.Equal([WebMcpConstants.ResourceNames.ChatBridgeScript], context.Scripts);
        Assert.Equal("/tenant/Communication/Hub/WebMcpHub", context.Settings["chatHubUrl"]);
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

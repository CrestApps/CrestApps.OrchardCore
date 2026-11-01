using System.Text.Json;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpBrowserToolFunctionTests
{
    private static readonly WebMcpBrowserTool _tool = new()
    {
        Name = "page_set_form_field",
        Title = "Set form field",
        Description = "Sets a field.",
        InputSchema = JsonDocument.Parse("""{"type":"object","properties":{"field":{"type":"string"}}}""").RootElement.Clone(),
    };

    [Fact]
    public void Function_DescribesThePageTool()
    {
        // Act
        var function = new WebMcpBrowserToolFunction(_tool, "connection-1", Mock.Of<IWebMcpBrowserToolInvoker>());

        // Assert
        Assert.Equal("page_set_form_field", function.Name);
        Assert.Equal("Sets a field.", function.Description);
        Assert.Equal("object", function.JsonSchema.GetProperty("type").GetString());
    }

    [Fact]
    public async Task InvokeAsync_RunsTheToolInTheTabWithTheArgumentsAsJson()
    {
        // Arrange
        string sentArguments = null;
        var invoker = new Mock<IWebMcpBrowserToolInvoker>();

        invoker
            .Setup(i => i.InvokeAsync("connection-1", "page_set_form_field", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string argumentsJson, CancellationToken _) => sentArguments = argumentsJson)
            .ReturnsAsync(WebMcpBrowserToolResult.Success("Done."));

        var function = new WebMcpBrowserToolFunction(_tool, "connection-1", invoker.Object);
        var arguments = new AIFunctionArguments
        {
            ["field"] = "Title",
            ["value"] = JsonDocument.Parse("42").RootElement,
        };

        // Act
        var result = await function.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Done.", result);

        using var document = JsonDocument.Parse(sentArguments);

        Assert.Equal("Title", document.RootElement.GetProperty("field").GetString());
        Assert.Equal(42, document.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_TellsTheModelWhenThePageToolFails()
    {
        // Arrange
        var invoker = new Mock<IWebMcpBrowserToolInvoker>();

        invoker
            .Setup(i => i.InvokeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebMcpBrowserToolResult.Failure("The page did not answer in time."));

        var function = new WebMcpBrowserToolFunction(_tool, "connection-1", invoker.Object);

        // Act
        var result = await function.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("The page tool failed: The page did not answer in time.", result);
    }
}

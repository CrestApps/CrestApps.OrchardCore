using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CrestApps.Core.AI;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.WebMcp.Endpoints;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

/// <summary>
/// The storefront tools are the selected tools marked safe for anonymous callers. The server offers nothing else to
/// anyone who calls the storefront endpoints, and offers nothing while the setting is off.
/// </summary>
public sealed class StorefrontWebMcpToolsTests
{
    [Fact]
    public void AnonymousAIToolOptions_MatchesMarkedNamesWithoutRegardToCase()
    {
        // Arrange
        var options = new AnonymousAIToolOptions();

        // Act
        options.MarkSafe(" searchStoreProducts ");

        // Assert
        Assert.True(options.IsSafe("searchstoreproducts"));
        Assert.False(options.IsSafe("deleteEverything"));
        Assert.False(options.IsSafe(null));
    }

    [Fact]
    public void MarkAIToolAnonymousSafe_AddsTheToolToTheOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.MarkAIToolAnonymousSafe(SafeTool.ToolName).MarkAIToolAnonymousSafe(OtherSafeTool.ToolName);

        // Assert
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AnonymousAIToolOptions>>().Value;

        Assert.Equal([OtherSafeTool.ToolName, SafeTool.ToolName], options.Tools.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GetTools_ListsOnlyTheSelectedSelectableToolsThatAreMarked()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [SafeTool.ToolName, UnsafeTool.ToolName, HiddenSafeTool.ToolName],
        });

        // Act
        var tools = service.GetTools();

        // Assert
        var tool = Assert.Single(tools);

        Assert.Equal(SafeTool.ToolName, tool.Name);
        Assert.Equal("Safe tool", tool.Title);
    }

    [Fact]
    public void GetTools_ListsEveryMarkedToolWhenAllToolsAreExposed()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var names = service.GetTools().Select(tool => tool.Name).Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal([OtherSafeTool.ToolName, SafeTool.ToolName], names);
    }

    [Fact]
    public async Task InvokeAsync_RefusesAToolThatIsNotMarkedEvenWhenItIsSelected()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await service.InvokeAsync(UnsafeTool.ToolName, new Dictionary<string, object>(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.NotFound, result.Status);
        Assert.Equal(0, UnsafeTool.Calls);
    }

    [Fact]
    public async Task InvokeAsync_RefusesAMarkedToolTheSettingsDoNotSelect()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { Tools = [SafeTool.ToolName] });

        // Act
        var result = await service.InvokeAsync(OtherSafeTool.ToolName, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task InvokeAsync_CallsAMarkedSelectedTool()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { Tools = [SafeTool.ToolName] });

        // Act
        var result = await service.InvokeAsync(SafeTool.ToolName, new Dictionary<string, object> { ["text"] = "hello" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.Succeeded, result.Status);
        Assert.Equal("safe: hello", result.Text);
    }

    [Fact]
    public void GetToolsEndpoint_AnswersNotFoundWhileTheStorefrontToolsAreOff()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = StorefrontWebMcpEndpoints.GetTools(PageOptions(enabled: false), service);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void GetToolsEndpoint_ListsTheStorefrontToolsToAnonymousVisitors()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = StorefrontWebMcpEndpoints.GetTools(PageOptions(enabled: true), service);

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolListResponse>>(result);

        Assert.DoesNotContain(ok.Value.Tools, tool => tool.Name == UnsafeTool.ToolName);
        Assert.Contains(ok.Value.Tools, tool => tool.Name == SafeTool.ToolName);
    }

    [Fact]
    public async Task InvokeEndpoint_AnswersNotFoundWhileTheStorefrontToolsAreOff()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await StorefrontWebMcpEndpoints.InvokeAsync(SafeTool.ToolName, AnonymousRequest("{}"), PageOptions(enabled: false), Antiforgery(valid: true), service);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task InvokeEndpoint_RefusesAnAnonymousCallToAToolThatIsNotMarked()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await StorefrontWebMcpEndpoints.InvokeAsync(UnsafeTool.ToolName, AnonymousRequest("{}"), PageOptions(enabled: true), Antiforgery(valid: true), service);

        // Assert
        Assert.IsType<NotFound>(result);
        Assert.Equal(0, UnsafeTool.Calls);
    }

    [Fact]
    public async Task InvokeEndpoint_AnswersBadRequestWithoutAValidAntiforgeryToken()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await StorefrontWebMcpEndpoints.InvokeAsync(SafeTool.ToolName, AnonymousRequest("{}"), PageOptions(enabled: true), Antiforgery(valid: false), service);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task InvokeEndpoint_AnswersBadRequestWhenTheInputIsNotAJsonObject()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await StorefrontWebMcpEndpoints.InvokeAsync(SafeTool.ToolName, AnonymousRequest("[1]"), PageOptions(enabled: true), Antiforgery(valid: true), service);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task InvokeEndpoint_CallsAMarkedToolForAnAnonymousVisitor()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions { ExposeAllTools = true });

        // Act
        var result = await StorefrontWebMcpEndpoints.InvokeAsync(SafeTool.ToolName, AnonymousRequest("""{ "text": "hi" }"""), PageOptions(enabled: true), Antiforgery(valid: true), service);

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolCallResponse>>(result);

        Assert.False(ok.Value.IsError);
        Assert.Equal("safe: hi", Assert.Single(ok.Value.Content).Text);
    }

    private static StorefrontWebMcpToolService CreateService(WebMcpOptions options)
    {
        var services = new ServiceCollection();

        services.AddCoreAITool<SafeTool>(SafeTool.ToolName).WithTitle("Safe tool").Selectable();
        services.AddCoreAITool<OtherSafeTool>(OtherSafeTool.ToolName).Selectable();
        services.AddCoreAITool<UnsafeTool>(UnsafeTool.ToolName).Selectable();

        // Marked, but a system tool, which is never offered outside the server.
        services.AddCoreAITool<HiddenSafeTool>(HiddenSafeTool.ToolName);

        services
            .MarkAIToolAnonymousSafe(SafeTool.ToolName)
            .MarkAIToolAnonymousSafe(OtherSafeTool.ToolName)
            .MarkAIToolAnonymousSafe(HiddenSafeTool.ToolName);

        var provider = services.BuildServiceProvider();
        var monitor = new Mock<IOptionsMonitor<WebMcpOptions>>();

        monitor
            .Setup(m => m.CurrentValue)
            .Returns(options);

        return new StorefrontWebMcpToolService(
            provider,
            provider.GetRequiredService<IOptions<CrestApps.Core.AI.Tooling.AIToolDefinitionOptions>>(),
            provider.GetRequiredService<IOptions<AnonymousAIToolOptions>>(),
            monitor.Object,
            NullLogger<StorefrontWebMcpToolService>.Instance);
    }

    private static IOptionsSnapshot<WebMcpPageOptions> PageOptions(bool enabled)
    {
        var snapshot = new Mock<IOptionsSnapshot<WebMcpPageOptions>>();

        snapshot
            .Setup(s => s.Value)
            .Returns(new WebMcpPageOptions { EnableStorefrontTools = enabled });

        return snapshot.Object;
    }

    private static IAntiforgery Antiforgery(bool valid)
    {
        var antiforgery = new Mock<IAntiforgery>();

        antiforgery
            .Setup(service => service.IsRequestValidAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(valid);

        return antiforgery.Object;
    }

    private static DefaultHttpContext AnonymousRequest(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };

        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = bytes.Length;
        httpContext.Request.ContentType = "application/json";

        return httpContext;
    }

    private abstract class EchoTool : AIFunction
    {
        public override JsonElement JsonSchema { get; } = JsonDocument.Parse("""{ "type": "object" }""").RootElement.Clone();

        protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            OnCalled();

            arguments.TryGetValue("text", out var value);

            var text = value is JsonElement element ? element.GetString() : value as string;

            return ValueTask.FromResult<object>($"{Prefix}: {text}");
        }

        protected abstract string Prefix { get; }

        protected virtual void OnCalled()
        {
        }
    }

    private sealed class SafeTool : EchoTool
    {
        public const string ToolName = "safeTool";

        public override string Name => ToolName;

        protected override string Prefix => "safe";
    }

    private sealed class OtherSafeTool : EchoTool
    {
        public const string ToolName = "otherSafeTool";

        public override string Name => ToolName;

        protected override string Prefix => "other";
    }

    private sealed class HiddenSafeTool : EchoTool
    {
        public const string ToolName = "hiddenSafeTool";

        public override string Name => ToolName;

        protected override string Prefix => "hidden";
    }

    private sealed class UnsafeTool : EchoTool
    {
        public const string ToolName = "unsafeTool";

        private static int _calls;

        public static int Calls => _calls;

        public override string Name => ToolName;

        protected override string Prefix => "unsafe";

        protected override void OnCalled()
            => Interlocked.Increment(ref _calls);
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CrestApps.OrchardCore.AI.WebMcp;
using CrestApps.OrchardCore.AI.WebMcp.Filters;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpScriptFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_AddsTheScriptAndItsConfigurationToAnAdminPage()
    {
        // Arrange
        var harness = new FilterHarness();
        var context = harness.CreateContext(isAdmin: true);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.True(harness.NextCalled);
        Assert.Equal([WebMcpConstants.ResourceNames.Script], harness.Scripts);

        var configuration = harness.ReadConfiguration();

        Assert.Equal("/tenant/api/ai/webmcp/tools", configuration.GetProperty("toolsUrl").GetString());
        Assert.Equal("/tenant/api/ai/webmcp/tools/{name}/invoke", configuration.GetProperty("invokeUrlTemplate").GetString());
        Assert.Equal("RequestVerificationToken", configuration.GetProperty("antiforgeryHeaderName").GetString());
        Assert.Equal("request-token", configuration.GetProperty("antiforgeryToken").GetString());
        Assert.True(configuration.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task OnResultExecutionAsync_LeavesFrontEndPagesAloneByDefault()
    {
        // Arrange
        var harness = new FilterHarness();
        var context = harness.CreateContext(isAdmin: false);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.True(harness.NextCalled);
        Assert.Empty(harness.Scripts);
        Assert.Empty(harness.HeadScripts);
    }

    [Fact]
    public async Task OnResultExecutionAsync_AddsTheScriptToAFrontEndPageWhenTurnedOn()
    {
        // Arrange
        var harness = new FilterHarness(new WebMcpOptions
        {
            EnableOnAdminPages = false,
            EnableOnFrontEndPages = true,
        });
        var context = harness.CreateContext(isAdmin: false);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.Equal([WebMcpConstants.ResourceNames.Script], harness.Scripts);
        Assert.False(harness.ReadConfiguration().GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task OnResultExecutionAsync_LeavesAnonymousVisitorsAlone()
    {
        // Arrange
        var harness = new FilterHarness();
        var context = harness.CreateContext(isAdmin: true, authenticated: false);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.True(harness.NextCalled);
        Assert.Empty(harness.Scripts);
    }

    [Fact]
    public async Task OnResultExecutionAsync_LeavesUsersWithoutThePermissionAlone()
    {
        // Arrange
        var harness = new FilterHarness(allowed: false);
        var context = harness.CreateContext(isAdmin: true);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.True(harness.NextCalled);
        Assert.Empty(harness.Scripts);
    }

    [Fact]
    public async Task OnResultExecutionAsync_LeavesResultsThatAreNotPagesAlone()
    {
        // Arrange
        var harness = new FilterHarness();
        var context = harness.CreateContext(isAdmin: true, result: new JsonResult(new { }));

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.True(harness.NextCalled);
        Assert.Empty(harness.Scripts);
    }

    [Fact]
    public async Task OnResultExecutionAsync_AddsWhatTheHandlersContribute()
    {
        // Arrange
        var handler = new Mock<IWebMcpClientConfigurationHandler>();

        handler
            .Setup(h => h.ConfigureAsync(It.IsAny<WebMcpClientConfigurationContext>()))
            .Callback((WebMcpClientConfigurationContext context) =>
            {
                context.AddScript("ExtraScript");
                context.AddScript("ExtraScript");
                context.Settings["extra"] = "</script><script>alert(1)</script>";
            })
            .Returns(Task.CompletedTask);

        var harness = new FilterHarness(handlers: [handler.Object]);
        var context = harness.CreateContext(isAdmin: true);

        // Act
        await harness.Filter.OnResultExecutionAsync(context, harness.Next);

        // Assert
        Assert.Equal([WebMcpConstants.ResourceNames.Script, "ExtraScript"], harness.Scripts);
        Assert.Single(harness.HeadScripts.Single().Split("</script>", StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("</script><script>alert(1)</script>", harness.ReadConfiguration().GetProperty("extra").GetString());
    }

    [Fact]
    public void BuildConfigurationElement_WritesAJsonBlockThePageCannotRun()
    {
        // Arrange
        var settings = new Dictionary<string, object>
        {
            ["value"] = "<b>&</b>",
        };

        // Act
        var element = WebMcpScriptFilter.BuildConfigurationElement(settings);

        // Assert
        Assert.StartsWith($"<script type=\"application/json\" id=\"{WebMcpConstants.ConfigurationElementId}\">", element, StringComparison.Ordinal);
        Assert.EndsWith("</script>", element, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", element, StringComparison.Ordinal);
    }

    private sealed class FilterHarness
    {
        private readonly List<string> _headScripts = [];

        public FilterHarness(WebMcpOptions options = null, bool allowed = true, IEnumerable<IWebMcpClientConfigurationHandler> handlers = null)
        {
            var snapshot = new Mock<IOptionsSnapshot<WebMcpOptions>>();

            snapshot
                .Setup(s => s.Value)
                .Returns(options ?? new WebMcpOptions());

            var authorizationService = new Mock<IAuthorizationService>();

            authorizationService
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var resourceManager = new Mock<IResourceManager>();

            resourceManager
                .Setup(manager => manager.RegisterResource("script", It.IsAny<string>()))
                .Callback((string _, string name) => Scripts.Add(name))
                .Returns(() => new RequireSettings());

            resourceManager
                .Setup(manager => manager.RegisterHeadScript(It.IsAny<IHtmlContent>()))
                .Callback((IHtmlContent content) =>
                {
                    using var writer = new StringWriter();

                    content.WriteTo(writer, HtmlEncoder.Default);
                    _headScripts.Add(writer.ToString());
                });

            var antiforgery = new Mock<IAntiforgery>();

            antiforgery
                .Setup(service => service.GetAndStoreTokens(It.IsAny<HttpContext>()))
                .Returns(new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "RequestVerificationToken"));

            Filter = new WebMcpScriptFilter(
                snapshot.Object,
                authorizationService.Object,
                resourceManager.Object,
                antiforgery.Object,
                handlers ?? []);
        }

        public WebMcpScriptFilter Filter { get; }

        public List<string> Scripts { get; } = [];

        public IReadOnlyList<string> HeadScripts => _headScripts;

        public bool NextCalled { get; private set; }

        public ResultExecutingContext CreateContext(bool isAdmin, bool authenticated = true, IActionResult result = null)
        {
            var identity = authenticated
                ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")
                : new ClaimsIdentity();

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            };

            httpContext.Request.PathBase = "/tenant";

            if (isAdmin)
            {
                httpContext.Items[typeof(AdminAttribute)] = null;
            }

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            return new ResultExecutingContext(actionContext, [], result ?? new ViewResult(), controller: new object());
        }

        public Task<ResultExecutedContext> Next()
        {
            NextCalled = true;

            return Task.FromResult<ResultExecutedContext>(null);
        }

        public JsonElement ReadConfiguration()
        {
            var element = Assert.Single(_headScripts);
            var start = element.IndexOf('>', StringComparison.Ordinal) + 1;
            var end = element.LastIndexOf("</script>", StringComparison.Ordinal);

            return JsonDocument.Parse(element.Substring(start, end - start)).RootElement;
        }
    }
}

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.WebMcp.Endpoints;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpToolEndpointsTests
{
    [Fact]
    public async Task GetToolsAsync_RefusesAnonymousUsers()
    {
        // Arrange
        var httpContext = CreateHttpContext(authenticated: false);
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.GetToolsAsync(httpContext, Authorization(true), toolService.Object);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetToolsAsync_RefusesUsersWithoutThePermission()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.GetToolsAsync(httpContext, Authorization(false), toolService.Object);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetToolsAsync_ListsTheUsersTools()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        var tools = new List<WebMcpToolDescriptor>
        {
            new()
            {
                Name = "echo_tool",
                Title = "Echo",
                Description = "Echoes.",
            },
        };

        var toolService = new Mock<IWebMcpToolService>();

        toolService
            .Setup(service => service.GetToolsAsync(httpContext.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        // Act
        var result = await WebMcpToolEndpoints.GetToolsAsync(httpContext, Authorization(true), toolService.Object);

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolListResponse>>(result);
        Assert.Same(tools, ok.Value.Tools);
    }

    [Fact]
    public async Task InvokeAsync_RefusesAnonymousUsers()
    {
        // Arrange
        var httpContext = CreateHttpContext(authenticated: false, body: "{}");
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_RefusesUsersWithoutThePermission()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: "{}");
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(false), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_RefusesARequestWithoutAValidAntiforgeryToken()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: "{}");
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(false), toolService.Object);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("{broken")]
    public async Task InvokeAsync_RefusesInputThatIsNotAJsonObject(string body)
    {
        // Arrange
        var httpContext = CreateHttpContext(body: body);
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_RefusesInputLargerThanTheLimit()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: "{}");
        httpContext.Request.ContentLength = WebMcpToolEndpoints.MaxRequestBodyLength + 1;
        var toolService = new Mock<IWebMcpToolService>();

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
        toolService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_AnswersNotFoundForAToolTheUserCannotCall()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: "{}");
        var toolService = new Mock<IWebMcpToolService>();

        toolService
            .Setup(service => service.InvokeAsync(httpContext.User, "missing", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebMcpToolInvocationResult.NotFound());

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("missing", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task InvokeAsync_CallsTheToolWithTheArgumentsAndAnswersAnMcpResult()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: """{"text":"hi","count":2}""");
        IDictionary<string, object> received = null;
        var toolService = new Mock<IWebMcpToolService>();

        toolService
            .Setup(service => service.InvokeAsync(httpContext.User, "echo_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback((ClaimsPrincipal _, string _, IDictionary<string, object> arguments, CancellationToken _) => received = arguments)
            .ReturnsAsync(WebMcpToolInvocationResult.Success("echo: hi"));

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolCallResponse>>(result);
        Assert.False(ok.Value.IsError);
        var content = Assert.Single(ok.Value.Content);
        Assert.Equal("text", content.Type);
        Assert.Equal("echo: hi", content.Text);
        Assert.Equal("hi", ((JsonElement)received["text"]).GetString());
        Assert.Equal(2, ((JsonElement)received["count"]).GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_TreatsAnEmptyBodyAsNoArguments()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: string.Empty);
        IDictionary<string, object> received = null;
        var toolService = new Mock<IWebMcpToolService>();

        toolService
            .Setup(service => service.InvokeAsync(httpContext.User, "echo_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback((ClaimsPrincipal _, string _, IDictionary<string, object> arguments, CancellationToken _) => received = arguments)
            .ReturnsAsync(WebMcpToolInvocationResult.Success("ok"));

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("echo_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        Assert.IsType<Ok<WebMcpToolCallResponse>>(result);
        Assert.NotNull(received);
        Assert.Empty(received);
    }

    [Fact]
    public async Task InvokeAsync_ReportsAFailedToolAsAnErrorResult()
    {
        // Arrange
        var httpContext = CreateHttpContext(body: "{}");
        var toolService = new Mock<IWebMcpToolService>();

        toolService
            .Setup(service => service.InvokeAsync(httpContext.User, "failing_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebMcpToolInvocationResult.Failure("The tool failed."));

        // Act
        var result = await WebMcpToolEndpoints.InvokeAsync("failing_tool", httpContext, Authorization(true), Antiforgery(true), toolService.Object);

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolCallResponse>>(result);
        Assert.True(ok.Value.IsError);
        Assert.Equal("The tool failed.", Assert.Single(ok.Value.Content).Text);
    }

    private static DefaultHttpContext CreateHttpContext(bool authenticated = true, string body = null)
    {
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")
            : new ClaimsIdentity();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);

            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.Body = new MemoryStream(bytes);
        }

        return httpContext;
    }

    private static IAuthorizationService Authorization(bool allowed)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static IAntiforgery Antiforgery(bool valid)
    {
        var antiforgery = new Mock<IAntiforgery>();

        antiforgery
            .Setup(service => service.IsRequestValidAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(valid);

        return antiforgery.Object;
    }
}

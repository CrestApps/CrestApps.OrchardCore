using CrestApps.OrchardCore.AI.Agent.Tools.Contents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class GetContentItemLinkToolTests
{
    [Fact]
    public async Task InvokeAsync_WithNullHttpContext_ShouldReturnFallbackMessage()
    {
        // Arrange: simulate a background task where HttpContext is null.
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(httpContextAccessor.Object);

        var tool = new GetContentItemLinkTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["contentItemId"] = "test-content-id",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: should return a descriptive fallback message, not throw NullReferenceException.
        Assert.NotNull(result);
        var text = result.ToString();
        Assert.Contains("test-content-id", text);
        Assert.Contains("background", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingContentItemId_ShouldReturnErrorMessage()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(httpContextAccessor.Object);

        var tool = new GetContentItemLinkTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("contentItemId", result.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithHttpContext_ShouldAttemptLinkGeneration()
    {
        // Arrange: simulate an HTTP request context.
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((ContentItem)null);

        var linkGenerator = new Mock<LinkGenerator>();

        var services = CreateServiceProvider(httpContextAccessor.Object, contentManager.Object, linkGenerator.Object);

        var tool = new GetContentItemLinkTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["contentItemId"] = "test-id",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: no NullReferenceException was thrown.
        Assert.NotNull(result);
    }

    private static ServiceProvider CreateServiceProvider(
        IHttpContextAccessor httpContextAccessor,
        IContentManager contentManager = null,
        LinkGenerator linkGenerator = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(httpContextAccessor);
        services.AddSingleton<ILogger<GetContentItemLinkTool>>(NullLogger<GetContentItemLinkTool>.Instance);

        if (contentManager is not null)
        {
            services.AddSingleton(contentManager);
        }
        else
        {
            services.AddSingleton(new Mock<IContentManager>().Object);
        }

        if (linkGenerator is not null)
        {
            services.AddSingleton(linkGenerator);
        }
        else
        {
            services.AddSingleton(new Mock<LinkGenerator>().Object);
        }

        return services.BuildServiceProvider();
    }
}

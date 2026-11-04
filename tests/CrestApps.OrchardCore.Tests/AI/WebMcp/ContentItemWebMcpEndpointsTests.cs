using System.Security.Claims;
using System.Text;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.Core.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.WebMcp.Endpoints;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class ContentItemWebMcpEndpointsTests
{
    [Fact]
    public async Task GetTools_AnswersUnauthorizedForAnonymousUsers()
    {
        // Arrange
        var harness = new Harness { Authenticated = false };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetTools_AnswersForbiddenWithoutTheWebMcpPermission()
    {
        // Arrange
        var harness = new Harness { CanUse = false };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task GetTools_AnswersNotFoundForAMissingOrUnpublishedItem()
    {
        // Arrange
        var harness = new Harness { ContentItem = null };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTools_AnswersNotFoundForAnItemWithoutThePart()
    {
        // Arrange
        var harness = new Harness { ContentItem = new ContentItem { ContentItemId = "item-1", ContentType = "Article" } };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTools_AnswersNotFoundForAnItemWithoutTools()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item() };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTools_AnswersNotFoundWhenTheUserMayNotViewTheItem()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles"), CanView = false };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTools_ListsTheToolsTheItemExposesToAUserWhoMayViewIt()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles") };

        // Act
        var result = await harness.GetToolsAsync();

        // Assert
        var ok = Assert.IsType<Ok<WebMcpToolListResponse>>(result);

        // No tool is registered in this test, so the list is empty; the checks before it passed.
        Assert.Empty(ok.Value.Tools);
    }

    [Fact]
    public async Task Invoke_AnswersBadRequestWithoutAValidAntiforgeryToken()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles"), AntiforgeryValid = false };

        // Act
        var result = await harness.InvokeAsync("listAIProfiles", "{}");

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task Invoke_AnswersBadRequestWhenTheInputIsNotAJsonObject()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles") };

        // Act
        var result = await harness.InvokeAsync("listAIProfiles", "[1, 2]");

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task Invoke_AnswersNotFoundForAToolTheItemDoesNotExpose()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles") };

        // Act
        var result = await harness.InvokeAsync("deleteEverything", "{}");

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Invoke_ChecksTheItemBeforeTheAntiforgeryToken()
    {
        // Arrange
        var harness = new Harness { ContentItem = Item("listAIProfiles"), CanView = false, AntiforgeryValid = false };

        // Act
        var result = await harness.InvokeAsync("listAIProfiles", "{}");

        // Assert
        Assert.IsType<NotFound>(result);
    }

    private static ContentItem Item(params string[] tools)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = "item-1",
            ContentType = "Article",
        };

        contentItem.Weld(nameof(WebMcpPart), new WebMcpPart
        {
            Tools = [.. tools],
        });

        return contentItem;
    }

    private sealed class Harness
    {
        public bool Authenticated { get; init; } = true;

        public bool CanUse { get; init; } = true;

        public bool CanView { get; init; } = true;

        public bool AntiforgeryValid { get; init; } = true;

        public ContentItem ContentItem { get; init; } = Item("listAIProfiles");

        public Task<IResult> GetToolsAsync()
        {
            var (httpContext, accessEvaluator, contentManager, authorizationService, _, factory) = Build(null);

            return ContentItemWebMcpEndpoints.GetToolsAsync("item-1", httpContext, accessEvaluator, contentManager, authorizationService, factory);
        }

        public Task<IResult> InvokeAsync(string name, string body)
        {
            var (httpContext, accessEvaluator, contentManager, authorizationService, antiforgery, factory) = Build(body);

            return ContentItemWebMcpEndpoints.InvokeAsync("item-1", name, httpContext, accessEvaluator, contentManager, authorizationService, antiforgery, factory);
        }

        private (HttpContext, IWebMcpAccessEvaluator, IContentManager, IAuthorizationService, IAntiforgery, ContentItemWebMcpToolServiceFactory) Build(string body)
        {
            var identity = Authenticated
                ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")
                : new ClaimsIdentity();

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            };

            if (body is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);

                httpContext.Request.Body = new MemoryStream(bytes);
                httpContext.Request.ContentLength = bytes.Length;
                httpContext.Request.ContentType = "application/json";
            }

            var accessEvaluator = new Mock<IWebMcpAccessEvaluator>();

            accessEvaluator
                .Setup(evaluator => evaluator.CanUseAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CanUse);

            var contentManager = new Mock<IContentManager>();

            contentManager
                .Setup(manager => manager.GetAsync("item-1", It.Is<VersionOptions>(options => options.IsPublished)))
                .ReturnsAsync(ContentItem);

            var authorizationService = new Mock<IAuthorizationService>();

            authorizationService
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(CanView ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var antiforgery = new Mock<IAntiforgery>();

            antiforgery
                .Setup(service => service.IsRequestValidAsync(It.IsAny<HttpContext>()))
                .ReturnsAsync(AntiforgeryValid);

            var toolAccessEvaluator = new Mock<IAIToolAccessEvaluator>();

            toolAccessEvaluator
                .Setup(evaluator => evaluator.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var factory = new ContentItemWebMcpToolServiceFactory(
                new ServiceCollection().BuildServiceProvider(),
                Options.Create(new AIToolDefinitionOptions()),
                toolAccessEvaluator.Object,
                NullLogger<DefaultWebMcpToolService>.Instance);

            return (httpContext, accessEvaluator.Object, contentManager.Object, authorizationService.Object, antiforgery.Object, factory);
        }
    }
}

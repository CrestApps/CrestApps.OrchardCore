using System.Security.Claims;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Json;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Mcp;

/// <summary>
/// SEC-A14: the MCP content resources returned any item, drafts included, to any MCP caller, with no content permission
/// check, and listed every item of a type at once.
/// </summary>
public sealed class ContentResourceAuthorizationTests
{
    [Fact]
    public async Task ContentById_WithoutViewContent_ReturnsNothing()
    {
        var item = new ContentItem { ContentItemId = "item-1", ContentType = "Page", Published = true, Latest = true };
        var handler = CreateByIdHandler(item, authorized: false);

        var result = await handler.ReadAsync(Resource(), new Dictionary<string, string> { ["contentItemId"] = "item-1" }, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("item-1\"", Text(result));
        Assert.Contains("not found", Text(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContentById_ADraftVersion_IsDenied()
    {
        var draft = new ContentItem { ContentItemId = "item-1", ContentItemVersionId = "draft-1", ContentType = "Page", Published = false, Latest = true };
        var handler = CreateByIdHandler(draft, authorized: true);

        var result = await handler.ReadAsync(Resource(), new Dictionary<string, string> { ["contentItemVersionId"] = "draft-1" }, TestContext.Current.CancellationToken);

        Assert.Contains("not found", Text(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContentById_APublishedItemTheCallerMayView_IsReturned()
    {
        var item = new ContentItem { ContentItemId = "item-1", ContentType = "Page", Published = true, Latest = true };
        var handler = CreateByIdHandler(item, authorized: true);

        var result = await handler.ReadAsync(Resource(), new Dictionary<string, string> { ["contentItemId"] = "item-1" }, TestContext.Current.CancellationToken);

        Assert.Contains("item-1", Text(result));
        Assert.DoesNotContain("not found", Text(result), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("100000")]
    public async Task ContentByType_RefusesAPageOutOfRange_WithoutQuerying(string page)
    {
        var session = new Mock<ISession>(MockBehavior.Strict);
        var handler = new ContentByTypeResourceTypeHandler(
            session.Object,
            Mock.Of<IAuthorizationService>(),
            new HttpContextAccessor(),
            Options.Create(new DocumentJsonSerializerOptions()),
            NullLogger<ContentByTypeResourceTypeHandler>.Instance);

        var result = await handler.ReadAsync(Resource(), new Dictionary<string, string> { ["contentType"] = "Page", ["page"] = page }, TestContext.Current.CancellationToken);

        Assert.Contains("page", Text(result), StringComparison.OrdinalIgnoreCase);
    }

    private static ContentByIdResourceTypeHandler CreateByIdHandler(ContentItem item, bool authorized)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(item.ContentItemId, It.IsAny<VersionOptions>())).ReturnsAsync(item.Published ? item : null);
        contentManager.Setup(manager => manager.GetVersionAsync(It.IsAny<string>())).ReturnsAsync(item);

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], "Api")) });

        return new ContentByIdResourceTypeHandler(
            contentManager.Object,
            authorizationService.Object,
            accessor.Object,
            Options.Create(new DocumentJsonSerializerOptions()),
            NullLogger<ContentByIdResourceTypeHandler>.Instance);
    }

    private static McpResource Resource()
        => new() { ItemId = "r1", Resource = new Resource { Uri = "content://item", Name = "item" } };

    private static string Text(ReadResourceResult result)
        => string.Concat(result.Contents.OfType<TextResourceContents>().Select(content => content.Text));
}

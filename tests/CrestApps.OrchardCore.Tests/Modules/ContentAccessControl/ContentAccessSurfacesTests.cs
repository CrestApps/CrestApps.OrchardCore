using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CrestApps.OrchardCore.ContentAccessControl.Feeds;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.ContentAccessControl.Indexing;
using CrestApps.OrchardCore.ContentAccessControl.Search;
using CrestApps.OrchardCore.ContentAccessControl.Sitemaps;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Feeds;
using OrchardCore.Feeds.Models;
using OrchardCore.Indexing;
using OrchardCore.Search.Abstractions;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContentAccessControl;

/// <summary>
/// SEC-P8: Content Access Control restricted only the item-level View content check, so a restricted item's title and
/// fields still showed in the sitemap, RSS feeds and search results, and to editors through preview and clone. Each of
/// those surfaces now leaves it out for whoever isn't in its roles (the sitemap, which is cached for everyone, always),
/// and the search index carries its roles.
/// </summary>
public sealed class ContentAccessSurfacesTests
{
    private static readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    [Theory]
    [InlineData("PreviewContent")]
    [InlineData("CloneContent")]
    public async Task PreviewAndClone_OfARestrictedItem_AreRefusedToANonMember(string permissionName)
    {
        var permission = permissionName == "PreviewContent" ? CommonPermissions.PreviewContent : CommonPermissions.CloneContent;
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], _anonymous, Restricted());

        context.Succeed(requirement);
        await new RoleBasedContentItemAuthorizationHandler(Services()).HandleAsync(context);

        Assert.True(context.HasFailed);
    }

    [Theory]
    [InlineData("PreviewContent")]
    [InlineData("PreviewOwnContent")]
    [InlineData("CloneContent")]
    [InlineData("CloneOwnContent")]
    public async Task PreviewAndClone_OfARestrictedItem_AreNotGrantedToAMemberWithoutThePermission(string permissionName)
    {
        // Arrange: a member of the item's role who was not given the permission by any role.
        var requirement = new PermissionRequirement(new Permission(permissionName));
        var context = new AuthorizationHandlerContext([requirement], Member(), Restricted());

        // Act
        await new RoleBasedContentItemAuthorizationHandler(Services()).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Viewing_ARestrictedItem_IsGrantedToAMember()
    {
        // Arrange
        var requirement = new PermissionRequirement(CommonPermissions.ViewContent);
        var context = new AuthorizationHandlerContext([requirement], Member(), Restricted());

        // Act
        await new RoleBasedContentItemAuthorizationHandler(Services()).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task TheFeedOfARestrictedList_IsEmptied_ForANonMember()
    {
        // Arrange: Orchard's list feed wrote the restricted container's title into the channel before any item.
        var channel = new XElement("channel", new XElement("title", "Members only"), new XElement("description", "Members' news"));
        var updater = new Mock<IUpdateModel>();
        updater.Setup(model => model.TryUpdateModelAsync(It.IsAny<It.IsAnyType>()))
            .Callback(new InvocationAction(invocation => invocation.Arguments[0].GetType().GetProperty("ContentItemId").SetValue(invocation.Arguments[0], "restricted")))
            .Returns(Task.FromResult(true));
        var context = new FeedContext(updater.Object, "rss") { Response = new FeedResponse { Element = channel } };
        var builder = new RestrictedContentFeedItemBuilder(new PassThroughFeedItemBuilder(), Contents(), Authorization(), Accessor(_anonymous));

        // Act
        await builder.PopulateAsync(context);

        // Assert
        Assert.DoesNotContain("Members", channel.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSitemap_LeavesOutARestrictedItem_ForEveryone()
    {
        var filter = new RestrictedContentSitemapFilter(Definitions());

        var restricted = await filter.ApplyExtendedMetadataAsync(null, null, Restricted(), new XElement("url"));
        var open = await filter.ApplyExtendedMetadataAsync(null, null, Open(), new XElement("url"));

        Assert.False(restricted);
        Assert.True(open);
    }

    [Fact]
    public async Task AFeed_LeavesOutARestrictedItem_ForANonMember()
    {
        // Arrange: the feed builder already wrote both items to the channel, as the RSS builder does.
        var channel = new XElement("channel");
        var context = new FeedContext(new Mock<IUpdateModel>().Object, "rss") { Response = new FeedResponse { Element = channel } };

        foreach (var item in new[] { Restricted(), Open() })
        {
            var element = new XElement("item", new XElement("title", item.DisplayText));

            channel.Add(element);
            context.Response.Items.Add(new FeedItem<ContentItem> { Item = item, Element = element });
        }

        var builder = new RestrictedContentFeedItemBuilder(new PassThroughFeedItemBuilder(), Contents(), Authorization(), Accessor(_anonymous));

        // Act
        await builder.PopulateAsync(context);

        // Assert
        var kept = Assert.Single(context.Response.Items.OfType<FeedItem<ContentItem>>());
        Assert.Equal("open", kept.Item.ContentItemId);
        Assert.DoesNotContain("Members only", channel.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchResults_LeaveOutARestrictedItem_ForANonMember()
    {
        // Arrange
        var inner = new Mock<ISearchService>();
        inner.Setup(service => service.SearchAsync(It.IsAny<global::OrchardCore.Indexing.Models.IndexProfile>(), "members", 0, 10))
            .ReturnsAsync(new SearchResult { Success = true, ContentItemIds = ["restricted", "open"], TotalCount = 2 });
        var contents = new Mock<IContentManager>();
        contents.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync([Restricted(), Open()]);
        var search = new RestrictedContentSearchService(inner.Object, contents.Object, Authorization(), Accessor(_anonymous));

        // Act
        var result = await search.SearchAsync(new global::OrchardCore.Indexing.Models.IndexProfile(), "members", 0, 10);

        // Assert
        Assert.Equal(["open"], result.ContentItemIds);
    }

    [Fact]
    public async Task TheSearchIndex_CarriesARestrictedItemsRoles()
    {
        var handler = new ContentAccessIndexHandler(Definitions());
        var restricted = new DocumentIndex("restricted");
        var open = new DocumentIndex("open");

        await handler.BuildIndexAsync(new BuildDocumentIndexContext(restricted, Restricted(), ["restricted"], new Mock<IContentIndexSettings>().Object));
        await handler.BuildIndexAsync(new BuildDocumentIndexContext(open, Open(), ["open"], new Mock<IContentIndexSettings>().Object));

        Assert.Contains(restricted.Entries, entry => entry.Name == ContentAccessIndexHandler.RoleNamesField && Equals(entry.Value, "Member"));
        Assert.DoesNotContain(open.Entries, entry => entry.Name == ContentAccessIndexHandler.RoleNamesField);
    }

    private static ClaimsPrincipal Member()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Member")], "Test"));

    private static IContentManager Contents()
    {
        var contents = new Mock<IContentManager>();
        contents.Setup(manager => manager.GetAsync("restricted", It.IsAny<VersionOptions>())).ReturnsAsync(Restricted());

        return contents.Object;
    }

    internal static ContentItem Restricted(string id = "restricted")
    {
        var item = new ContentItem { ContentItemId = id, ContentType = "MembersArticle", DisplayText = "Members only" };

        item.Weld(new RolePickerPart { RoleNames = ["Member"] });

        return item;
    }

    internal static ContentItem Open(string id = "open")
        => new() { ContentItemId = id, ContentType = "Article", DisplayText = "For everyone" };

    private static IContentDefinitionManager Definitions()
    {
        var settings = new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { [nameof(RolePickerPartContentAccessControlSettings.IsContentRestricted)] = true },
        };
        var restricted = new ContentTypeDefinition("MembersArticle", "Members' Article", [new ContentTypePartDefinition(nameof(RolePickerPart), new ContentPartDefinition(nameof(RolePickerPart)), settings)], new JsonObject());
        var open = new ContentTypeDefinition("Article", "Article", [], new JsonObject());
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(manager => manager.GetTypeDefinitionAsync("MembersArticle")).ReturnsAsync(restricted);
        definitions.Setup(manager => manager.GetTypeDefinitionAsync("Article")).ReturnsAsync(open);

        return definitions.Object;
    }

    private static ServiceProvider Services()
        => new ServiceCollection().AddSingleton(Definitions()).BuildServiceProvider();

    // Everyone may view content; the Content Access Control handler refuses what is restricted.
    internal static IAuthorizationService Authorization()
        => new HandlerAuthorizationService(new RoleBasedContentItemAuthorizationHandler(Services()));

    internal static HttpContextAccessor Accessor(ClaimsPrincipal user)
        => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };

    private sealed class HandlerAuthorizationService : IAuthorizationService
    {
        private readonly IAuthorizationHandler _handler;

        public HandlerAuthorizationService(IAuthorizationHandler handler) => _handler = handler;

        public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var context = new AuthorizationHandlerContext(requirements, user, resource);

            foreach (var requirement in context.PendingRequirements.ToList())
            {
                context.Succeed(requirement);
            }

            await _handler.HandleAsync(context);

            return context.HasFailed ? AuthorizationResult.Failed() : AuthorizationResult.Success();
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class PassThroughFeedItemBuilder : IFeedItemBuilder
    {
        public Task PopulateAsync(FeedContext context) => Task.CompletedTask;
    }
}

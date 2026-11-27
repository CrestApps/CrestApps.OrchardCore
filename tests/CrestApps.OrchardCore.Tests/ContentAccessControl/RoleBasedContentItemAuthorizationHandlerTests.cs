using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.ContentAccessControl;

/// <summary>
/// An item restricted to roles opens only for people in one of them. The Authenticated role stands for anyone signed
/// in and the Anonymous role for visitors, though neither is ever a role claim.
/// </summary>
public sealed class RoleBasedContentItemAuthorizationHandlerTests
{
    private const string ContentType = "Handbook";

    [Fact]
    public async Task AnItemForAuthenticated_OpensForAnyoneSignedIn_WithoutARoleClaim()
    {
        // Arrange
        var item = Item("Authenticated");

        // Act
        var context = await AuthorizeAsync(SignedIn(), item);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task AnItemForAuthenticated_IsRefusedToAVisitor()
    {
        // Arrange
        var item = Item("Authenticated");

        // Act
        var context = await AuthorizeAsync(Visitor(), item, succeededBefore: true);

        // Assert
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task AnItemForAnonymous_OpensForAVisitor()
    {
        // Arrange
        var item = Item("Anonymous");

        // Act
        var context = await AuthorizeAsync(Visitor(), item);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task AnItemForAnonymous_IsRefusedToSomeoneSignedIn()
    {
        // Arrange
        var item = Item("Anonymous");

        // Act
        var context = await AuthorizeAsync(SignedIn(), item, succeededBefore: true);

        // Assert
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task AnItemForAnonymousAndAuthenticated_OpensForEveryone()
    {
        // Arrange
        var item = Item("Anonymous", "Authenticated");

        // Act
        var visitor = await AuthorizeAsync(Visitor(), item);
        var signedIn = await AuthorizeAsync(SignedIn(), item);

        // Assert
        Assert.True(visitor.HasSucceeded);
        Assert.True(signedIn.HasSucceeded);
    }

    [Fact]
    public async Task AnItemForAnotherRole_IsStillRefusedToSomeoneSignedIn_WithoutIt()
    {
        // Arrange
        var item = Item("Member");

        // Act
        var context = await AuthorizeAsync(SignedIn(), item, succeededBefore: true);

        // Assert
        Assert.True(context.HasFailed);
    }

    [Theory]
    [InlineData("ViewContent_Handbook")]
    [InlineData("ViewOwnContent_Handbook")]
    [InlineData("PreviewContent_Handbook")]
    [InlineData("PreviewOwnContent")]
    [InlineData("ListContent")]
    public async Task ARestrictedItem_OnASecurableType_StaysRestricted_ForEveryPermissionThatReadsIt(string permission)
    {
        // Arrange: a securable type is also checked with its own view permission, which a visitor may hold through the type.
        var item = Item("Member");

        // Act
        var visitor = await AuthorizeAsync(Visitor(), item, succeededBefore: true, permission: Permission(permission));
        var signedIn = await AuthorizeAsync(SignedIn(), item, succeededBefore: true, permission: Permission(permission));

        // Assert
        Assert.True(visitor.HasFailed);
        Assert.True(signedIn.HasFailed);
    }

    [Fact]
    public async Task AMember_OpensARestrictedItem_ThroughTheTypesOwnViewPermission()
    {
        // Arrange
        var item = Item("Member");

        // Act
        var context = await AuthorizeAsync(SignedIn("Member"), item, permission: Permission("ViewContent_Handbook"));

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Theory]
    [InlineData("ListContent")]
    [InlineData("PreviewContent_Handbook")]
    [InlineData("ViewOwnContent_Handbook")]
    public async Task BeingInAPickedRole_DoesNotGrantListingOrPreviewing(string permission)
    {
        // Arrange: a picked role only lets its people view the item; listing and previewing still need their permission.
        var item = Item("Authenticated");

        // Act
        var context = await AuthorizeAsync(SignedIn(), item, permission: Permission(permission));

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    // C8 (review 11): the editor shows every field, and publishing or deleting changes the item, so they are refused too.
    [Theory]
    [InlineData("EditContent")]
    [InlineData("EditContent_Handbook")]
    [InlineData("PublishContent_Handbook")]
    [InlineData("DeleteOwnContent")]
    public async Task ARestriction_RefusesChangingTheItem_ToWhoeverIsntInItsRoles(string permission)
    {
        // Arrange
        var item = Item("Member");

        // Act
        var outsider = await AuthorizeAsync(SignedIn(), item, permission: Permission(permission));
        var member = await AuthorizeAsync(SignedIn("Member"), item, permission: Permission(permission));

        // Assert: a member isn't refused, and isn't granted the change either.
        Assert.True(outsider.HasFailed);
        Assert.False(member.HasFailed);
        Assert.False(member.HasSucceeded);
    }

    /// <summary>
    /// Round 2, #12: an audit trail snapshot of an item from before it was restricted was checked against its own roles,
    /// so a user outside the item's current roles could open it, or restore it and remove the restriction.
    /// </summary>
    [Theory]
    [InlineData("ViewContent")]
    [InlineData("EditContent")]
    [InlineData("PublishContent")]
    public async Task AnOlderVersion_IsCheckedAgainstTheItemsCurrentRestrictionToo(string permission)
    {
        // Arrange: the snapshot isn't restricted; the item now is, to Member.
        var snapshot = Item();
        snapshot.ContentItemVersionId = "version-1";

        var current = Item("Member");
        current.ContentItemVersionId = "version-2";

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("handbook", It.IsAny<VersionOptions>())).ReturnsAsync(current);

        // Act
        var outsider = await AuthorizeAsync(SignedIn(), snapshot, permission: Permission(permission), contentManager: contentManager.Object);
        var member = await AuthorizeAsync(SignedIn("Member"), snapshot, permission: Permission(permission), contentManager: contentManager.Object);

        // Assert
        Assert.True(outsider.HasFailed);
        Assert.False(member.HasFailed);
    }

    /// <summary>
    /// Round 3: the current roles were cached once a request without the version checked first, so checking the
    /// published version and then the draft dropped the published version's roles from the draft's check.
    /// </summary>
    [Fact]
    public async Task ADraft_IsCheckedAgainstThePublishedRoles_WhicheverVersionWasCheckedFirst()
    {
        // Arrange: the live page is restricted to Member; the draft widens it to Authenticated.
        var published = Item("Member");
        published.ContentItemVersionId = "version-1";
        published.Latest = false;

        var draft = Item("Authenticated");
        draft.ContentItemVersionId = "version-2";
        draft.Published = false;

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("handbook", It.Is<VersionOptions>(options => options.IsPublished))).ReturnsAsync(published);
        contentManager.Setup(manager => manager.GetAsync("handbook", It.Is<VersionOptions>(options => options.IsLatest))).ReturnsAsync(draft);

        var handler = new RoleBasedContentItemAuthorizationHandler(Services(contentManager: contentManager.Object));
        var user = SignedIn();

        // Act: the same handler (one a request) checks the live version first, then the draft.
        await HandleAsync(handler, user, published, CommonPermissions.EditContent);
        var context = await HandleAsync(handler, user, draft, CommonPermissions.EditContent);

        // Assert
        Assert.True(context.HasFailed);
    }

    /// <summary>
    /// Round 3: a draft that restricts the item to a role refused the live page to everyone outside it before the draft
    /// was published.
    /// </summary>
    [Fact]
    public async Task ADraftsRestriction_DoesNotApplyToTheLivePage_UntilItIsPublished()
    {
        // Arrange: the live page is open to everyone signed in; the draft restricts it to Member.
        var published = Item("Authenticated");
        published.ContentItemVersionId = "version-1";
        published.Latest = false;

        var draft = Item("Member");
        draft.ContentItemVersionId = "version-2";
        draft.Published = false;

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("handbook", It.Is<VersionOptions>(options => options.IsPublished))).ReturnsAsync(published);
        contentManager.Setup(manager => manager.GetAsync("handbook", It.Is<VersionOptions>(options => options.IsLatest))).ReturnsAsync(draft);

        // Act
        var viewing = await AuthorizeAsync(SignedIn(), published, contentManager: contentManager.Object);
        var editing = await AuthorizeAsync(SignedIn(), published, permission: CommonPermissions.EditContent, contentManager: contentManager.Object);

        // Assert: viewing the live page is allowed; changing the item still meets the draft's restriction.
        Assert.True(viewing.HasSucceeded);
        Assert.False(viewing.HasFailed);
        Assert.True(editing.HasFailed);
    }

    [Fact]
    public async Task ARestriction_LeavesPermissionsThatAreNotAboutTheItemAlone()
    {
        // Act
        var context = await AuthorizeAsync(SignedIn(), Item("Member"), permission: Permission("ViewContentTypes"));

        // Assert
        Assert.False(context.HasFailed);
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData("ViewContent", true)]
    [InlineData("ViewContent_KnowledgeArticle", true)]
    [InlineData("ViewOwnContent_KnowledgeArticle", true)]
    [InlineData("PreviewContent", true)]
    [InlineData("PreviewOwnContent_KnowledgeArticle", true)]
    [InlineData("ListContent", true)]
    [InlineData("EditContent", true)]
    [InlineData("PublishContent_KnowledgeArticle", true)]
    [InlineData("DeleteOwnContent", true)]
    [InlineData("ViewContentTypes", false)]
    [InlineData(null, false)]
    public void RestrictedPermissions_AreMatchedByName_InGeneralOrForAType(string permission, bool restricted)
        => Assert.Equal(restricted, RoleBasedContentItemAuthorizationHandler.IsRestricted(permission));

    private static global::OrchardCore.Security.Permissions.Permission Permission(string name)
        => new(name, global::OrchardCore.Localization.LocalizationSource.Create<RoleBasedContentItemAuthorizationHandlerTests>("Test permission"));

    internal static ClaimsPrincipal Visitor()
        => new(new ClaimsIdentity());

    internal static ClaimsPrincipal SignedIn(params string[] roles)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "dana"), .. roles.Select(role => new Claim(ClaimTypes.Role, role))], "Test"));

    internal static ContentItem Item(params string[] roles)
    {
        var item = new ContentItem { ContentItemId = "handbook", ContentType = ContentType, Published = true, Latest = true };

        item.Weld(new RolePickerPart { RoleNames = roles });

        return item;
    }

    internal static IServiceProvider Services(bool restricted = true, IContentManager contentManager = null)
    {
        var settings = new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { [nameof(RolePickerPartContentAccessControlSettings.IsContentRestricted)] = restricted },
        };
        var type = new ContentTypeDefinition(ContentType, "Handbook", [new ContentTypePartDefinition(nameof(RolePickerPart), new ContentPartDefinition(nameof(RolePickerPart)), settings)], new JsonObject());
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(manager => manager.GetTypeDefinitionAsync(ContentType)).ReturnsAsync(type);

        var services = new ServiceCollection().AddSingleton(definitions.Object);

        if (contentManager is not null)
        {
            services.AddSingleton(contentManager);
        }

        return services.BuildServiceProvider();
    }

    internal static async Task<AuthorizationHandlerContext> AuthorizeAsync(ClaimsPrincipal user, ContentItem item, bool succeededBefore = false, global::OrchardCore.Security.Permissions.Permission permission = null, IContentManager contentManager = null)
    {
        var requirement = new PermissionRequirement(permission ?? CommonPermissions.ViewContent);
        var context = new AuthorizationHandlerContext([requirement], user, item);

        if (succeededBefore)
        {
            context.Succeed(requirement);
        }

        await new RoleBasedContentItemAuthorizationHandler(Services(contentManager: contentManager)).HandleAsync(context);

        return context;
    }

    private static async Task<AuthorizationHandlerContext> HandleAsync(RoleBasedContentItemAuthorizationHandler handler, ClaimsPrincipal user, ContentItem item, global::OrchardCore.Security.Permissions.Permission permission)
    {
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, item);

        await handler.HandleAsync(context);

        return context;
    }
}

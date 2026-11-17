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

    internal static IServiceProvider Services(bool restricted = true)
    {
        var settings = new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { [nameof(RolePickerPartContentAccessControlSettings.IsContentRestricted)] = restricted },
        };
        var type = new ContentTypeDefinition(ContentType, "Handbook", [new ContentTypePartDefinition(nameof(RolePickerPart), new ContentPartDefinition(nameof(RolePickerPart)), settings)], new JsonObject());
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(manager => manager.GetTypeDefinitionAsync(ContentType)).ReturnsAsync(type);

        return new ServiceCollection().AddSingleton(definitions.Object).BuildServiceProvider();
    }

    internal static async Task<AuthorizationHandlerContext> AuthorizeAsync(ClaimsPrincipal user, ContentItem item, bool succeededBefore = false, global::OrchardCore.Security.Permissions.Permission permission = null)
    {
        var requirement = new PermissionRequirement(permission ?? CommonPermissions.ViewContent);
        var context = new AuthorizationHandlerContext([requirement], user, item);

        if (succeededBefore)
        {
            context.Succeed(requirement);
        }

        await new RoleBasedContentItemAuthorizationHandler(Services()).HandleAsync(context);

        return context;
    }
}

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

namespace CrestApps.OrchardCore.Tests.Memberships;

/// <summary>
/// Content restricted to roles is refused to whoever isn't in them, even when View content was granted to everyone.
/// </summary>
public sealed class RoleBasedContentAccessTests
{
    [Fact]
    public async Task RestrictedContent_IsRefused_ToANonMember_EvenAfterViewContentWasGranted()
    {
        var context = await AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), succeededBefore: true);

        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task RestrictedContent_IsShown_ToAMember()
    {
        var member = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Member")], "Test"));
        var context = await AuthorizeAsync(member, succeededBefore: false);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(ClaimsPrincipal user, bool succeededBefore)
    {
        var item = new ContentItem { ContentItemId = "article", ContentType = "MembersArticle" };

        item.Weld(new RolePickerPart { RoleNames = ["Member"] });

        var settings = new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { [nameof(RolePickerPartContentAccessControlSettings.IsContentRestricted)] = true },
        };
        var type = new ContentTypeDefinition("MembersArticle", "Members' Article", [new ContentTypePartDefinition(nameof(RolePickerPart), new ContentPartDefinition(nameof(RolePickerPart)), settings)], new JsonObject());
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(manager => manager.GetTypeDefinitionAsync("MembersArticle")).ReturnsAsync(type);

        var services = new ServiceCollection().AddSingleton(definitions.Object).BuildServiceProvider();
        var requirement = new PermissionRequirement(CommonPermissions.ViewContent);
        var context = new AuthorizationHandlerContext([requirement], user, item);

        if (succeededBefore)
        {
            context.Succeed(requirement);
        }

        await new RoleBasedContentItemAuthorizationHandler(services).HandleAsync(context);

        return context;
    }
}

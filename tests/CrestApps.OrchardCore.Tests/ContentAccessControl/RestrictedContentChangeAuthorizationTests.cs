using System.Security.Claims;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Contents.Security;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.ContentAccessControl;

/// <summary>
/// SEC-P8 (review 11, C8): an item restricted to roles can't be opened for editing, published or deleted by someone
/// outside its roles, whatever content permission they hold. These run the real authorization service with Orchard
/// Core's permission handler (which grants what a user's permission claims give) and its content type handler (which
/// asks again for the type's own and the owner's variants), as the content pages ask.
/// </summary>
public sealed class RestrictedContentChangeAuthorizationTests
{
    public static TheoryData<string> ChangingPermissions => new()
    {
        CommonPermissions.EditContent.Name,
        CommonPermissions.EditOwnContent.Name,
        CommonPermissions.PublishContent.Name,
        CommonPermissions.PublishOwnContent.Name,
        CommonPermissions.DeleteContent.Name,
        CommonPermissions.DeleteOwnContent.Name,
    };

    [Theory]
    [MemberData(nameof(ChangingPermissions))]
    public async Task AnEditorOutsideTheItemsRoles_CantChangeIt(string permissionName)
    {
        // Arrange: an editor holding every content permission, the owner's own included, and none of the item's roles.
        var authorization = Create();
        var editor = CalendarTestHarness.User("dana", [.. ChangingPermissions]);
        var item = RoleBasedContentItemAuthorizationHandlerTests.Item("Member");

        item.Owner = "dana";

        // Act
        var allowed = await authorization.AuthorizeAsync(editor, Permission(permissionName), item);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [MemberData(nameof(ChangingPermissions))]
    public async Task AnEditorInOneOfTheItemsRoles_StillChangesIt_WithThePermission(string permissionName)
    {
        // Arrange
        var authorization = Create();
        var editor = CalendarTestHarness.User("dana", [.. ChangingPermissions, "role:Member"]);
        var item = RoleBasedContentItemAuthorizationHandlerTests.Item("Member");

        item.Owner = "dana";

        // Act
        var allowed = await authorization.AuthorizeAsync(editor, Permission(permissionName), item);

        // Assert
        Assert.True(allowed);
    }

    [Theory]
    [MemberData(nameof(ChangingPermissions))]
    public async Task BeingInOneOfTheItemsRoles_GrantsNoChangeOnItsOwn(string permissionName)
    {
        // Arrange: a member with no content permission.
        var authorization = Create();
        var member = CalendarTestHarness.User("dana", "role:Member");
        var item = RoleBasedContentItemAuthorizationHandlerTests.Item("Member");

        item.Owner = "dana";

        // Act
        var allowed = await authorization.AuthorizeAsync(member, Permission(permissionName), item);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task AnItemWithoutRestriction_StaysOpenToItsEditors()
    {
        // Arrange
        var authorization = Create();
        var editor = CalendarTestHarness.User("dana", CommonPermissions.EditContent.Name, CommonPermissions.PublishContent.Name, CommonPermissions.DeleteContent.Name);
        var item = RoleBasedContentItemAuthorizationHandlerTests.Item();

        // Act and assert
        Assert.True(await authorization.AuthorizeAsync(editor, CommonPermissions.EditContent, item));
        Assert.True(await authorization.AuthorizeAsync(editor, CommonPermissions.PublishContent, item));
        Assert.True(await authorization.AuthorizeAsync(editor, CommonPermissions.DeleteContent, item));
    }

    // The permission objects Orchard Core's content pages ask with.
    private static Permission Permission(string name)
        => name switch
        {
            "EditContent" => CommonPermissions.EditContent,
            "EditOwnContent" => CommonPermissions.EditOwnContent,
            "PublishContent" => CommonPermissions.PublishContent,
            "PublishOwnContent" => CommonPermissions.PublishOwnContent,
            "DeleteContent" => CommonPermissions.DeleteContent,
            "DeleteOwnContent" => CommonPermissions.DeleteOwnContent,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

    private static IAuthorizationService Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAuthorizationCore();
        services.AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, ContentTypeAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, RoleBasedContentItemAuthorizationHandler>();
        services.AddSingleton(RoleBasedContentItemAuthorizationHandlerTests.Services().GetRequiredService<IContentDefinitionManager>());

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();
    }
}

using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Handlers;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class LocalPermissionDenyAuthorizationHandlerTests
{
    [Fact]
    public async Task DeniedPermission_ForALocalUser_Fails()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild("ManageRecipes"));
        var context = CreateContext("ManageRecipes", LocalUser());

        // Act
        await handler.HandleAsync(context);

        // Assert: a failure wins over any handler that grants the permission, such as the administrator's.
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task DeniedPermission_ComparesNamesIgnoringCase()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild("managerecipes"));
        var context = CreateContext("ManageRecipes", LocalUser());

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task DeniedPermission_ForSomeoneFromTheParent_IsLeftToTheOtherHandlers()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild("ManageRecipes"));
        var context = CreateContext("ManageRecipes", DelegatedUser());

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task OtherPermission_ForALocalUser_IsLeftToTheOtherHandlers()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild("ManageRecipes"));
        var context = CreateContext("ManageUsers", LocalUser());

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task NoDeniedPermissions_NeverFails()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild());
        var context = CreateContext("ManageRecipes", LocalUser());

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task AnonymousVisitor_IsLeftToTheOtherHandlers()
    {
        // Arrange
        var handler = new LocalPermissionDenyAuthorizationHandler(CreateChild("ManageRecipes"));
        var context = CreateContext("ManageRecipes", new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        await handler.HandleAsync(context);

        // Assert: an anonymous visitor has nothing to deny; the permission check refuses them anyway.
        Assert.False(context.HasFailed);
    }

    private static ShellSettings CreateChild(params string[] deniedPermissions)
    {
        var child = TestShellSettings.Child("client", TestShellSettings.Parent("firm"));
        TenantHierarchySettingsWriter.WriteChildPolicy(child, new ParentTenantPolicy { DeniedLocalPermissions = deniedPermissions });

        return child;
    }

    private static AuthorizationHandlerContext CreateContext(string permission, ClaimsPrincipal user)
        => new([new PermissionRequirement(new Permission(permission))], user, resource: null);

    private static ClaimsPrincipal LocalUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "local-admin")], "Identity.Application"));

    private static ClaimsPrincipal DelegatedUser()
        => new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "linked-user"),
                new Claim(TenantHierarchyConstants.ClaimTypes.SessionId, "session-id"),
            ],
            "Identity.Application"));
}

using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// The tool that changes the roles of a user makes the checks of the role editor and refuses what a chat must
/// not do: changing the roles of the caller, giving a role the caller may not assign, and taking the
/// administrator role from the last enabled administrator.
/// </summary>
public sealed class SetUserRolesToolTests
{
    [Fact]
    public async Task SetUserRoles_AddsAndRemovesRolesAndEndsTheSessionsOfTheUser()
    {
        // Arrange
        var harness = new UserRoleToolHarness();
        var stamp = harness.Jane.SecurityStamp;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["add"] = new[] { "author", "Moderator" },
            ["remove"] = new[] { "Editor", "Administrator" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var roleChecks = services.GetRequiredService<FakeAuthorizationService>().Checks
            .Where(check => check.Resource is IRole)
            .Select(check => $"{check.Permission} on {((IRole)check.Resource).RoleName}")
            .ToArray();

        Assert.Equal(new[] { "Author", "Moderator" }, harness.Jane.RoleNames);
        Assert.Equal(new[] { "Author", "Moderator" }, json.GetProperty("added").EnumerateArray().Select(name => name.GetString()).ToArray());
        Assert.Equal("Editor", Assert.Single(json.GetProperty("removed").EnumerateArray()).GetString());

        // A role the user did not have is reported, not treated as an error.
        Assert.Equal("Administrator", Assert.Single(json.GetProperty("unchanged").EnumerateArray()).GetString());

        // The permission to assign is checked on each role the call touches, as the role editor does.
        Assert.Equal(
            new[] { "AssignRoleToUsers on Author", "AssignRoleToUsers on Moderator", "AssignRoleToUsers on Editor", "AssignRoleToUsers on Administrator" },
            roleChecks);

        // A removed role changes the security stamp, which signs the user out.
        Assert.NotEqual(stamp, harness.Jane.SecurityStamp);
        Assert.Equal("user-jane", Assert.Single(harness.Store.Updated));
        Assert.DoesNotContain(harness.Jane.SecurityStamp, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetUserRoles_OnTheCallersOwnAccount_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = AgentToolTestContext.UserId,
            ["add"] = new[] { "Administrator" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: this tool does not change the roles of your own account", result, StringComparison.Ordinal);
        Assert.Empty(harness.Caller.RoleNames);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserRoles_ForARoleTheCallerMayNotAssign_RefusesTheWholeCall()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // The caller may assign every role except Moderator.
            Authorize = (permission, resource) => !(permission.Name == "AssignRoleToUsers" && resource is IRole { RoleName: "Moderator" }),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["add"] = new[] { "Author", "Moderator" },
        });

        // Assert
        Assert.Equal("Nothing was changed: you are not allowed to assign the role 'Moderator'.", result);
        Assert.Equal(new[] { "Editor" }, harness.Jane.RoleNames);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("Authenticated")]
    [InlineData("Anonymous")]
    [InlineData("Publisher")]
    public async Task SetUserRoles_ForARoleAUserCannotHold_Refuses(string roleName)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["add"] = new[] { roleName },
        });

        // Assert
        Assert.StartsWith($"Nothing was changed: '{roleName}' cannot be given to or taken from a user.", result, StringComparison.Ordinal);
        Assert.Contains("Administrator, Author, Editor, Moderator", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserRoles_RemovingTheAdministratorRoleFromTheOnlyEnabledAdministrator_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-root",
            ["remove"] = new[] { "Administrator" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: 'root' is the only enabled administrator", result, StringComparison.Ordinal);
        Assert.Equal(new[] { "Administrator" }, harness.Root.RoleNames);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserRoles_RemovingTheAdministratorRole_IsAllowedWhenAnotherAdministratorIsEnabled()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.FormerAdministrator.IsEnabled = true;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-root",
            ["remove"] = new[] { "Administrator" },
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("changed").GetBoolean());
        Assert.Empty(harness.Root.RoleNames);
    }

    [Fact]
    public async Task SetUserRoles_KeepsTheLastAdministratorOfASiteThatRenamedTheAdministratorRole()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        // The name of the administrator role is configurable. Here it is Editor, and Jane is its only enabled user.
        harness.Roles.AdministratorRoleName = "Editor";

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["remove"] = new[] { "Editor" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: 'jane' is the only enabled administrator", result, StringComparison.Ordinal);
        Assert.Equal(new[] { "Editor" }, harness.Jane.RoleNames);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserRoles_WhenTheUserManagerRefusesTheUser_PutsTheRolesBack()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Store.Failure = new IdentityError { Code = "ConcurrencyFailure", Description = "The user was changed by someone else." };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserRolesTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["add"] = new[] { "Author" },
        });

        // Assert
        Assert.Equal("Nothing was changed: The user was changed by someone else.", result);
        Assert.Equal(new[] { "Editor" }, harness.Jane.RoleNames);
    }
}

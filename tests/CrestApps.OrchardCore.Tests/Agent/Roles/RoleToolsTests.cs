using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.Tests.Agent.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Agent.Roles;

/// <summary>
/// The role tools do what the Roles admin page does, through the role manager, and refuse what a chat must not do:
/// changing the permissions of the administrator role, deleting a system role, giving a security-critical permission
/// to every visitor or every signed-in user, raising the caller's own access, and giving a security-critical
/// permission that the call does not name.
/// </summary>
public sealed class RoleToolsTests
{
    [Fact]
    public async Task ListRoles_ReturnsEveryRoleWithItsSystemAndAdministratorMarkers()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRolesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var roles = json.GetProperty("items").EnumerateArray()
            .Select(role => $"{role.GetProperty("name").GetString()}: system {role.GetProperty("isSystemRole").GetBoolean()}, administrator {role.GetProperty("isAdminRole").GetBoolean()}")
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Administrator: system True, administrator True",
                "Anonymous: system True, administrator False",
                "Authenticated: system True, administrator False",
                "Author: system False, administrator False",
                "Editor: system False, administrator False",
                "Moderator: system False, administrator False",
            },
            roles);

        Assert.Equal(6, json.GetProperty("total").GetInt32());
        Assert.Equal("Edits content.", json.GetProperty("items")[4].GetProperty("description").GetString());
    }

    [Fact]
    public async Task GetRole_ReturnsTheAssignedTheEffectiveAndTheInheritedPermissions()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // What the authorization service answers for a user who holds only the Editor role.
            Authorize = (permission, _) => permission.Name is "ViewRoles" or "EditContent" or "ViewContent" or "ViewOwnProfile",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRoleDetailsTool(), services, new()
        {
            ["roleName"] = "editor",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("Editor", json.GetProperty("role").GetProperty("name").GetString());
        Assert.False(json.GetProperty("role").GetProperty("isSystemRole").GetBoolean());
        Assert.Equal(new[] { "EditContent", "RetiredPermission" }, Names(json, "assignedPermissions"));
        Assert.Equal(new[] { "EditContent", "ViewContent", "ViewOwnProfile" }, Names(json, "effectivePermissions"));
        Assert.Equal(new[] { "ViewContent" }, Names(json, "inheritedFromAnonymous"));
        Assert.Equal(new[] { "ViewOwnProfile" }, Names(json, "inheritedFromAuthenticated"));
        Assert.Equal(new[] { "RetiredPermission" }, Names(json, "notProvidedByAnEnabledFeature"));
        Assert.Empty(Names(json, "securityCriticalPermissions"));
    }

    [Fact]
    public async Task GetRole_NamesTheSecurityCriticalPermissionsOfTheRole()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name is "ViewRoles" or "ManageUsers" or "EditUsers" or "View Users",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRoleDetailsTool(), services, new()
        {
            ["roleName"] = "Moderator",
            ["filter"] = "users",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "ManageUsers" }, Names(json, "assignedPermissions"));
        Assert.Equal(new[] { "EditUsers", "ManageUsers", "View Users" }, Names(json, "effectivePermissions"));
        Assert.Equal(new[] { "EditUsers", "ManageUsers" }, Names(json, "securityCriticalPermissions"));
    }

    [Fact]
    public async Task GetRole_ForTheAdministratorRole_SaysItHoldsEveryPermission()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRoleDetailsTool(), services, new()
        {
            ["roleName"] = "Administrator",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("holdsEveryPermission").GetBoolean());
        Assert.True(json.GetProperty("role").GetProperty("isAdminRole").GetBoolean());
        Assert.False(json.TryGetProperty("effectivePermissions", out _));
    }

    [Fact]
    public async Task GetRole_ForARoleThatDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRoleDetailsTool(), services, new()
        {
            ["roleName"] = "Publisher",
        });

        // Assert
        Assert.Equal("There is no role named 'Publisher'. Use listRoles to get the role names.", result);
    }

    [Fact]
    public async Task ListPermissions_ReturnsThePermissionsOfTheEnabledFeaturesWithTheirGroupAndRisk()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListPermissionsTool(), services, new()
        {
            ["pageSize"] = 50,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var editUsers = items.Single(item => item.GetProperty("name").GetString() == "EditUsers");
        var editContent = items.Single(item => item.GetProperty("name").GetString() == "EditContent");

        Assert.Equal(7, json.GetProperty("total").GetInt32());
        Assert.Equal(7, items.Count);
        Assert.Equal("Edit any user", editUsers.GetProperty("description").GetString());
        Assert.True(editUsers.GetProperty("securityCritical").GetBoolean());
        Assert.Equal("ManageUsers", Assert.Single(editUsers.GetProperty("impliedBy").EnumerateArray()).GetString());
        Assert.Equal("Contents", editContent.GetProperty("group").GetString());
        Assert.Equal("OrchardCore.Contents", editContent.GetProperty("feature").GetString());
        Assert.False(editContent.TryGetProperty("securityCritical", out _));
    }

    [Fact]
    public async Task ListPermissions_WithAFilter_ReturnsTheMatchingPermissionsAPageAtATime()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListPermissionsTool(), services, new()
        {
            ["filter"] = "content",
            ["pageSize"] = 2,
            ["page"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        // EditContent, PublishContent and ViewContent match; the group of every permission is Contents, so the
        // permissions for users match too.
        Assert.Equal(7, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("items").GetArrayLength());
        Assert.True(json.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task ListPermissions_ForSecurityCriticalOnly_ReturnsThose()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListPermissionsTool(), services, new()
        {
            ["securityCriticalOnly"] = true,
        });

        // Assert
        Assert.Equal(new[] { "EditUsers", "ManageUsers" }, AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray());
    }

    [Fact]
    public async Task SaveRole_AddsPermissionsAndKeepsTheOthers()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "editor",
            ["permissions"] = new[] { "publishcontent", "EditContent" },
            ["description"] = "Edits and publishes content.",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("created").GetBoolean());

        // The stored name is the name the feature gives the permission, whatever casing the call used.
        Assert.Equal(new[] { "PublishContent" }, Names(json, "added"));
        Assert.Equal(new[] { "EditContent", "RetiredPermission", "PublishContent" }, harness.GetPermissions("Editor"));
        Assert.Equal("Edits and publishes content.", harness.GetRole("Editor").RoleDescription);
        Assert.Equal("Editor", Assert.Single(harness.Roles.Updated));
    }

    [Fact]
    public async Task SaveRole_RemovesPermissions()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editor",
            ["permissions"] = new[] { "EditContent", "PublishContent" },
            ["permissionMode"] = "remove",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "EditContent" }, Names(json, "removed"));
        Assert.Equal(new[] { "PublishContent" }, Names(json, "notAssigned"));
        Assert.Equal(new[] { "RetiredPermission" }, harness.GetPermissions("Editor"));
    }

    [Fact]
    public async Task SaveRole_ReplacesThePermissionsAndKeepsTheOtherClaimsOfTheRole()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.GetRole("Editor").RoleClaims.Add(new RoleClaim("Department", "Marketing"));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editor",
            ["permissions"] = new[] { "ViewContent" },
            ["permissionMode"] = "replace",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "ViewContent" }, Names(json, "added"));
        Assert.Equal(new[] { "EditContent", "RetiredPermission" }, Names(json, "removed"));
        Assert.Equal(new[] { "ViewContent" }, harness.GetPermissions("Editor"));
        Assert.Contains(harness.GetRole("Editor").RoleClaims, claim => claim.ClaimType == "Department" && claim.ClaimValue == "Marketing");
    }

    [Fact]
    public async Task SaveRole_CreatesARoleThatCopiesThePermissionsOfAnotherRole()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = " Newsletter Manager ",
            ["create"] = true,
            ["description"] = "Sends the newsletter.",
            ["copyPermissionsFrom"] = "Editor",
            ["permissions"] = new[] { "PublishContent" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("Newsletter Manager", json.GetProperty("role").GetProperty("name").GetString());
        Assert.Equal(new[] { "EditContent", "PublishContent" }, harness.GetPermissions("Newsletter Manager"));
        Assert.Equal("Sends the newsletter.", harness.GetRole("Newsletter Manager").RoleDescription);

        // A permission that no enabled feature provides is left out of a copy, as on the Clone page.
        Assert.Equal(new[] { "RetiredPermission" }, Names(json, "notCopied"));
        Assert.Equal("Newsletter Manager", Assert.Single(harness.Roles.Created));
    }

    [Theory]
    [InlineData("Editor", "a role named 'Editor' exists")]
    [InlineData("Sales/West", "cannot contain a slash")]
    public async Task SaveRole_CreatingARoleWithANameTheAdminPageRefuses_Refuses(string roleName, string reason)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = roleName,
            ["create"] = true,
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains(reason, result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_UpdatingARoleThatDoesNotExist_RefusesInsteadOfCreatingIt()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editors",
            ["permissions"] = new[] { "EditContent" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: there is no role named 'Editors'", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_ChangingThePermissionsOfTheAdministratorRole_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Administrator",
            ["permissions"] = new[] { "EditContent" },
            ["permissionMode"] = "replace",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the administrator role holds every permission and its permissions cannot be changed", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_ChangingTheDescriptionOfTheAdministratorRole_IsAllowed()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Administrator",
            ["description"] = "The owners of the site.",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Equal("The owners of the site.", harness.GetRole("Administrator").RoleDescription);
        Assert.Empty(harness.GetPermissions("Administrator"));
    }

    [Fact]
    public async Task SaveRole_ForAPermissionNoEnabledFeatureProvides_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Author",
            ["permissions"] = new[] { "EditContent", "DoAnything" },
        });

        // Assert
        Assert.Equal("Nothing was saved: no enabled feature provides the permission 'DoAnything'. Use listPermissions to get the exact names.", result);
        Assert.Empty(harness.GetPermissions("Author"));
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("Anonymous", "visitor")]
    [InlineData("Authenticated", "signed-in user")]
    public async Task SaveRole_GivingASecurityCriticalPermissionToEveryone_RefusesEvenWhenItIsNamed(string roleName, string who)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // An administrator, so only the rule for these two roles can refuse the call.
            CallerRoleClaims = ["Administrator"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = roleName,
            ["permissions"] = new[] { "EditUsers" },
            ["securityCriticalPermissions"] = new[] { "EditUsers" },
        });

        // Assert
        Assert.Equal($"Nothing was saved: 'EditUsers' is security critical and cannot be given to the role '{roleName}', which every {who} holds. Give it to a role that is assigned to chosen users.", result);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_GivingASecurityCriticalPermissionTheCallDoesNotName_RefusesAndNamesIt()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Author",
            ["permissions"] = new[] { "EditContent", "ManageUsers", "EditUsers" },
            ["securityCriticalPermissions"] = new[] { "EditUsers" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: this call would give the role 'Author' the security-critical permission 'ManageUsers'.", result, StringComparison.Ordinal);
        Assert.Contains("securityCriticalPermissions", result, StringComparison.Ordinal);
        Assert.Empty(harness.GetPermissions("Author"));
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_GivingSecurityCriticalPermissionsTheCallNames_SavesAndReportsThem()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Author",
            ["permissions"] = new[] { "EditContent", "EditUsers" },
            ["securityCriticalPermissions"] = new[] { "editusers" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "EditUsers" }, Names(json, "securityCriticalGranted"));
        Assert.Equal(new[] { "EditContent", "EditUsers" }, harness.GetPermissions("Author"));
    }

    [Fact]
    public async Task SaveRole_KeepingASecurityCriticalPermissionTheRoleHas_NeedsNoConfirmation()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act: Moderator holds ManageUsers already; the call adds a permission that is not security critical.
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Moderator",
            ["permissions"] = new[] { "ManageUsers", "ViewContent" },
            ["permissionMode"] = "replace",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "ViewContent" }, Names(json, "added"));
        Assert.False(json.TryGetProperty("securityCriticalGranted", out _));
        Assert.Equal(new[] { "ManageUsers", "ViewContent" }, harness.GetPermissions("Moderator"));
    }

    [Theory]
    [InlineData("Editor")]
    [InlineData("Authenticated")]
    [InlineData("Anonymous")]
    public async Task SaveRole_AddingPermissionsToARoleTheCallerHolds_Refuses(string roleName)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // Every signed-in user holds Authenticated and what Anonymous has.
            CallerRoleClaims = ["Editor"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = roleName,
            ["permissions"] = new[] { "PublishContent" },
        });

        // Assert
        Assert.StartsWith($"Nothing was saved: you hold the role '{roleName}', and this tool does not add permissions to a role of the person who asks.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_AddingPermissionsToARoleTheCallerGotAfterSigningIn_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        // The role is stored on the account, and the sign-in of the caller is older than that.
        harness.Caller.RoleNames.Add("Editor");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editor",
            ["permissions"] = new[] { "PublishContent" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: you hold the role 'Editor'", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRole_RemovingPermissionsFromARoleTheCallerHolds_IsAllowed()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            CallerRoleClaims = ["Editor"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editor",
            ["permissions"] = new[] { "EditContent" },
            ["permissionMode"] = "remove",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Equal(new[] { "RetiredPermission" }, harness.GetPermissions("Editor"));
    }

    [Fact]
    public async Task SaveRole_ForAnAdministrator_AddsPermissionsToARoleTheAdministratorHolds()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // An administrator holds every permission already, so nothing is raised.
            CallerRoleClaims = ["Administrator", "Editor"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Editor",
            ["permissions"] = new[] { "PublishContent" },
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Contains("PublishContent", harness.GetPermissions("Editor"));
    }

    [Fact]
    public async Task SaveRole_WhenTheRoleManagerRefuses_ReportsTheError()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Roles.Failure = new IdentityError { Code = "Failed", Description = "The roles could not be stored." };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRoleTool(), services, new()
        {
            ["roleName"] = "Author",
            ["permissions"] = new[] { "EditContent" },
        });

        // Assert
        Assert.Equal("Nothing was saved: The roles could not be stored.", result);
        Assert.Empty(harness.GetPermissions("Author"));
    }

    [Fact]
    public async Task DeleteRole_DeletesThroughTheRoleManagerAndCountsTheUsersThatHadTheRole()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRoleTool(), services, new()
        {
            ["roleName"] = "editor",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("Editor", json.GetProperty("roleName").GetString());
        Assert.Equal(1, json.GetProperty("usersThatHadTheRole").GetInt32());
        Assert.Equal("Editor", Assert.Single(harness.Roles.Deleted));
        Assert.Null(harness.GetRole("Editor"));
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Anonymous")]
    [InlineData("Authenticated")]
    public async Task DeleteRole_ForASystemRole_Refuses(string roleName)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRoleTool(), services, new()
        {
            ["roleName"] = roleName,
        });

        // Assert
        Assert.Equal($"Nothing was deleted: '{roleName}' is a system role, and system roles cannot be deleted.", result);
        Assert.NotNull(harness.GetRole(roleName));
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteRole_ForARoleThatDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRoleTool(), services, new()
        {
            ["roleName"] = "Publisher",
        });

        // Assert
        Assert.StartsWith("Nothing was deleted: there is no role named 'Publisher'", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    private static string[] Names(JsonElement json, string property)
        => json.GetProperty(property).EnumerateArray().Select(name => name.GetString()).ToArray();
}

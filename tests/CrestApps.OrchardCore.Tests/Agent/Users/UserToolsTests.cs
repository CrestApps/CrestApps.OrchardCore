using CrestApps.OrchardCore.AI.Agent.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.ViewModels;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// The tools that list, read and update a user do what the Users admin page does, through the same services:
/// the filter text of the page, the fields a result may hold, and the site settings that lock a field.
/// </summary>
public sealed class UserToolsTests
{
    [Fact]
    public async Task ListUsers_WithFilters_RunsTheFilterTextOfTheAdminPageAndReturnsTheListFields()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.QueryResult = [harness.Jane, harness.Sam];

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUsersTool(), services, new()
        {
            ["text"] = "ja",
            ["email"] = "contoso.com",
            ["role"] = "editor",
            ["status"] = "disabled",
            ["sort"] = "email",
            ["pageSize"] = 10,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("email:contoso.com role:Editor status:Disabled sort:Email ja", json.GetProperty("filter").GetString());
        Assert.Equal(UsersFilter.Disabled, harness.QueriedOptions.Filter);
        Assert.Equal(UsersOrder.Email, harness.QueriedOptions.Order);
        Assert.Equal("Editor", harness.QueriedOptions.SelectedRole);
        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(10, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, items.Count);
        Assert.Equal("user-jane", items[0].GetProperty("userId").GetString());
        Assert.Equal("jane@contoso.com", items[0].GetProperty("email").GetString());
        Assert.Equal("Editor", Assert.Single(items[0].GetProperty("roles").EnumerateArray()).GetString());
        Assert.True(items[0].GetProperty("enabled").GetBoolean());
        Assert.False(items[1].GetProperty("enabled").GetBoolean());

        string[] expected = ["email", "enabled", "roles", "userId", "userName"];

        Assert.All(items, item => Assert.Equal(expected, item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray()));
    }

    [Fact]
    public async Task ListUsers_ForARoleThatDoesNotExist_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUsersTool(), services, new()
        {
            ["role"] = "Publisher",
        });

        // Assert
        Assert.StartsWith("Nothing was listed: there is no role named 'Publisher'", result, StringComparison.Ordinal);
        Assert.Null(harness.QueriedOptions);
    }

    [Fact]
    public async Task ListUsers_ForARoleWhoseUsersTheCallerMayNotList_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name != "ListUsersInRole_Editor",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUsersTool(), services, new()
        {
            ["role"] = "Editor",
        });

        // Assert
        Assert.StartsWith("Nothing was listed: you are not allowed to list the users of the role 'Editor'", result, StringComparison.Ordinal);
        Assert.Null(harness.QueriedOptions);
    }

    [Fact]
    public async Task ListUsers_LeavesOutTheUsersTheCallerMayNotList()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // The caller may list editors only: the permission is refused on every other user.
            Authorize = (_, resource) => resource is not User user || user.UserId is null || user.RoleNames.Contains("Editor"),
        };

        harness.QueryResult = [harness.Jane, harness.Sam, harness.Root];

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUsersTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("user-jane", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("userId").GetString());
        Assert.Equal(2, json.GetProperty("notListed").GetInt32());
        Assert.DoesNotContain("sam@fabrikam.com", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text", "email:contoso.com")]
    [InlineData("email", "a b")]
    [InlineData("status", "pending")]
    [InlineData("sort", "created")]
    public async Task ListUsers_WithAFilterValueTheParserWouldMisread_Refuses(string argument, string value)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUsersTool(), services, new()
        {
            [argument] = value,
        });

        // Assert
        Assert.StartsWith("Nothing was listed:", result, StringComparison.Ordinal);
        Assert.Null(harness.QueriedOptions);
    }

    [Fact]
    public async Task GetUser_ReturnsTheProfileTheRolesAndTheSignInState()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            ["userId"] = "user-jane",
        });

        // Assert
        var user = AgentToolTestContext.ReadJson(result).GetProperty("user");

        Assert.Equal("jane", user.GetProperty("userName").GetString());
        Assert.Equal("jane@contoso.com", user.GetProperty("email").GetString());
        Assert.False(user.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal("+14155550100", user.GetProperty("phoneNumber").GetString());
        Assert.True(user.GetProperty("enabled").GetBoolean());
        Assert.Equal("Editor", Assert.Single(user.GetProperty("roles").EnumerateArray()).GetString());
        Assert.False(user.GetProperty("isSiteOwner").GetBoolean());
        Assert.True(user.GetProperty("hasPassword").GetBoolean());
        Assert.True(user.GetProperty("twoFactorEnabled").GetBoolean());

        // The lockout state is the lockout end in the future, not the flag that says the account can be locked out.
        Assert.True(user.GetProperty("lockedOut").GetBoolean());
        Assert.Equal("2099-01-01T00:00:00+00:00", user.GetProperty("lockedOutUntil").GetString());
        Assert.Equal(5, user.GetProperty("failedSignInCount").GetInt32());
        Assert.Equal("Google", Assert.Single(user.GetProperty("externalLoginProviders").EnumerateArray()).GetString());
        Assert.Equal("Europe/Paris", user.GetProperty("timeZoneId").GetString());
        Assert.Equal("fr-FR", user.GetProperty("culture").GetString());
        Assert.Equal("Sales", user.GetProperty("customSettings").GetProperty("UserProfile").GetProperty("UserProfile").GetProperty("Department").GetProperty("Text").GetString());
        Assert.False(user.GetProperty("customSettings").GetProperty("UserProfile").TryGetProperty("ContentItemId", out _));
        Assert.Equal("UTC", user.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task GetUser_ForAUserWhoCanBeLockedOutButIsNot_SaysNotLockedOut()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Sam.IsLockoutEnabled = true;
        harness.Sam.LockoutEndUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            ["userId"] = "user-sam",
        });

        // Assert
        var user = AgentToolTestContext.ReadJson(result).GetProperty("user");

        Assert.False(user.GetProperty("lockedOut").GetBoolean());
        Assert.False(user.TryGetProperty("lockedOutUntil", out _));
        Assert.False(user.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task GetUser_ForTheCallersOwnAccount_NeedsNoPermission()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            ["userId"] = AgentToolTestContext.UserId,
        });

        // Assert
        Assert.Equal(AgentToolTestContext.UserName, AgentToolTestContext.ReadJson(result).GetProperty("user").GetProperty("userName").GetString());
    }

    [Fact]
    public async Task GetUser_ForAUserThatDoesNotExist_SaysSoToACallerWhoMayViewUsers()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            ["email"] = "nobody@contoso.com",
        });

        // Assert
        Assert.StartsWith("There is no user with the email 'nobody@contoso.com'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUser_ReturnsTheCustomSettingsOfATypeOnlyToACallerWhoHoldsItsPermission()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name != "ManageOwnCustomUserSettings_UserProfile",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            ["userId"] = "user-jane",
        });

        // Assert
        Assert.False(AgentToolTestContext.ReadJson(result).GetProperty("user").TryGetProperty("customSettings", out _));
        Assert.DoesNotContain("Sales", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveUser_ChangesTheGivenFieldsThroughTheUserManager()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["userName"] = "jane.doe",
            ["email"] = "jane.doe@contoso.com",
            ["phoneNumber"] = "",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(new[] { "userName", "email", "phoneNumber" }, json.GetProperty("changed").EnumerateArray().Select(name => name.GetString()).ToArray());
        Assert.Equal("jane.doe", harness.Jane.UserName);
        Assert.Equal("JANE.DOE", harness.Jane.NormalizedUserName);
        Assert.Equal("jane.doe@contoso.com", harness.Jane.Email);
        Assert.Equal(string.Empty, harness.Jane.PhoneNumber);
        Assert.Equal("user-jane", Assert.Single(harness.Store.Updated));
    }

    [Fact]
    public async Task SaveUser_WithoutAUserId_DoesNotCreateAUser()
    {
        // Arrange
        var harness = new UserRoleToolHarness();
        var users = harness.Store.Users.Count;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userName"] = "maria",
            ["email"] = "maria@contoso.com",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: give the userId", result, StringComparison.Ordinal);
        Assert.Contains("does not create a user", result, StringComparison.Ordinal);
        Assert.Equal(users, harness.Store.Users.Count);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("userName", "jane.doe", "user name")]
    [InlineData("email", "jane.doe@contoso.com", "email address")]
    [InlineData("phoneNumber", "+14155550199", "phone number")]
    public async Task SaveUser_ForAFieldTheSiteLocks_RefusesTheFieldAndSavesNothing(string argument, string value, string field)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            LoginSettings = new LoginSettings
            {
                AllowChangingUsername = argument != "userName",
                AllowChangingEmail = argument != "email",
                AllowChangingPhoneNumber = argument != "phoneNumber",
            },
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            [argument] = value,
        });

        // Assert
        Assert.Equal($"Nothing was saved: this site does not allow changing {(field == "email address" ? "an" : "a")} {field}. An administrator can allow it in Settings > Security > User Login.", result);
        Assert.Equal("jane", harness.Jane.UserName);
        Assert.Equal("jane@contoso.com", harness.Jane.Email);
        Assert.Equal("+14155550100", harness.Jane.PhoneNumber);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUser_WithAPhoneNumberThatIsNotValid_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["phoneNumber"] = "call me",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: 'call me' is not a valid phone number", result, StringComparison.Ordinal);
        Assert.Equal("+14155550100", harness.Jane.PhoneNumber);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUser_WhenTheUserManagerRefusesTheUser_ReportsWhyAndPutsTheValuesBack()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act: another user has the user name already.
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["userName"] = "sam",
            ["email"] = "jane.doe@contoso.com",
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains("'sam' is already taken", result, StringComparison.Ordinal);
        Assert.Equal("jane", harness.Jane.UserName);
        Assert.Equal("jane@contoso.com", harness.Jane.Email);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUser_OnTheCallersOwnAccount_NeedsThePermissionToEditOwnUserInformation()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name != "ManageOwnUserInformation",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = AgentToolTestContext.UserId,
            ["email"] = "new@contoso.com",
        });

        // Assert
        Assert.Equal("Nothing was saved: you are not allowed to change your own user information.", result);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveUser_OnTheCallersOwnAccount_SavesAndSaysHowTheSessionGetsTheNewValues()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUserTool(), services, new()
        {
            ["userId"] = AgentToolTestContext.UserId,
            ["email"] = "new@contoso.com",
        });

        // Assert
        Assert.Equal("new@contoso.com", harness.Caller.Email);
        Assert.Contains("Sign out and sign in again", AgentToolTestContext.ReadJson(result).GetProperty("note").GetString(), StringComparison.Ordinal);
    }
}

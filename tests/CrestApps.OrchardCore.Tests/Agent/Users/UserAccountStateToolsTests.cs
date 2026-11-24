using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// The tools that enable, disable, unlock and delete a user, confirm an email and reset the second sign-in factors
/// authentication call the services the Users admin page calls, and refuse the account of the caller, the site
/// owner and the last enabled administrator.
/// </summary>
public sealed class UserAccountStateToolsTests
{
    [Fact]
    public async Task SetUserAccountState_Enable_EnablesThroughTheUserServiceAndReportsEachUser()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-sam", "user-jane", "user-missing", "user-sam" },
            ["action"] = "enable",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var outcomes = json.GetProperty("results").EnumerateArray()
            .Select(row => $"{row.GetProperty("userId").GetString()}: {row.GetProperty("outcome").GetString()}")
            .ToArray();

        Assert.Equal(new[] { "user-sam: enabled", "user-jane: alreadyEnabled", "user-missing: notFound" }, outcomes);
        Assert.Equal("enable", json.GetProperty("action").GetString());
        Assert.Equal(1, json.GetProperty("changedCount").GetInt32());
        Assert.True(harness.Sam.IsEnabled);
        Assert.Equal("user-sam", Assert.Single(harness.Enabled));
    }

    [Fact]
    public async Task SetUserAccountState_ForAUserTheCallerMayNotEdit_AnswersAsForAnIdThatDoesNotExist()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (_, resource) => resource is not User { UserId: "user-sam" },
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-sam" },
            ["action"] = "enable",
        });

        // Assert
        var row = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("results").EnumerateArray());

        Assert.Equal("notFound", row.GetProperty("outcome").GetString());
        Assert.False(row.TryGetProperty("userName", out _));
        Assert.False(harness.Sam.IsEnabled);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserAccountState_Disable_RefusesTheCallerTheSiteOwnerAndTheOnlyEnabledAdministrator_AndDisablesTheOthers()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { AgentToolTestContext.UserId, UserRoleToolHarness.OwnerId, "user-root", "user-sam", "user-jane" },
            ["action"] = "disable",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var rows = json.GetProperty("results").EnumerateArray().ToList();
        var outcomes = rows
            .Select(row => $"{row.GetProperty("userId").GetString()}: {row.GetProperty("outcome").GetString()}")
            .ToArray();

        Assert.Equal(
            new[] { "user-1: refused", "user-owner: refused", "user-root: refused", "user-sam: alreadyDisabled", "user-jane: disabled" },
            outcomes);

        Assert.Contains("your own account", rows[0].GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains("site owner", rows[1].GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains("only enabled administrator", rows[2].GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.True(harness.Caller.IsEnabled);
        Assert.True(harness.Owner.IsEnabled);
        Assert.True(harness.Root.IsEnabled);
        Assert.False(harness.Jane.IsEnabled);
        Assert.Equal("user-jane", Assert.Single(harness.Disabled));
        Assert.Equal(1, json.GetProperty("changedCount").GetInt32());
    }

    [Fact]
    public async Task SetUserAccountState_Disable_ForAnAdministrator_IsAllowedWhenAnotherAdministratorIsEnabled()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.FormerAdministrator.IsEnabled = true;

        using var services = harness.Build();

        // Act: after the first one is disabled, the second is the only enabled administrator.
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-root", "user-former" },
            ["action"] = "disable",
        });

        // Assert
        var outcomes = AgentToolTestContext.ReadJson(result).GetProperty("results").EnumerateArray()
            .Select(row => $"{row.GetProperty("userId").GetString()}: {row.GetProperty("outcome").GetString()}")
            .ToArray();

        Assert.Equal(new[] { "user-root: disabled", "user-former: refused" }, outcomes);
        Assert.True(harness.FormerAdministrator.IsEnabled);
    }

    [Fact]
    public async Task SetUserAccountState_Unlock_ClearsTheLockoutAndTheFailedSignIns()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-jane" },
            ["action"] = "unlock",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var row = Assert.Single(json.GetProperty("results").EnumerateArray());

        Assert.Equal("unlocked", row.GetProperty("outcome").GetString());
        Assert.Equal(5, row.GetProperty("clearedFailedSignInCount").GetInt32());
        Assert.Equal(1, json.GetProperty("changedCount").GetInt32());
        Assert.Null(harness.Jane.LockoutEndUtc);
        Assert.Equal(0, harness.Jane.AccessFailedCount);
    }

    [Fact]
    public async Task SetUserAccountState_Unlock_ForAUserWhoIsNotLockedOut_ResetsTheFailedSignInsOnly()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Sam.AccessFailedCount = 2;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-sam" },
            ["action"] = "unlock",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var row = Assert.Single(json.GetProperty("results").EnumerateArray());

        Assert.Equal("notLockedOut", row.GetProperty("outcome").GetString());
        Assert.Equal(2, row.GetProperty("clearedFailedSignInCount").GetInt32());
        Assert.Equal(0, json.GetProperty("changedCount").GetInt32());
        Assert.Equal(0, harness.Sam.AccessFailedCount);
    }

    [Fact]
    public async Task SetUserAccountState_ConfirmEmail_ConfirmsWithATokenThatStaysOnTheServer()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-jane" },
            ["action"] = "confirmEmail",
        });

        // Assert
        var row = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("results").EnumerateArray());

        Assert.Equal("confirmed", row.GetProperty("outcome").GetString());
        Assert.True(harness.Jane.EmailConfirmed);
        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetUserAccountState_ConfirmEmail_ForAnEmailThatIsConfirmed_ChangesNothing()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Jane.EmailConfirmed = true;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = new[] { "user-jane" },
            ["action"] = "confirmEmail",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var row = Assert.Single(json.GetProperty("results").EnumerateArray());

        Assert.Equal("alreadyConfirmed", row.GetProperty("outcome").GetString());
        Assert.Equal(0, json.GetProperty("changedCount").GetInt32());
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("lock")]
    public async Task SetUserAccountState_WithAnUnknownAction_ChangesNothing(string action)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["userIds"] = new[] { "user-jane" },
        };

        if (action is not null)
        {
            arguments["action"] = action;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was changed: action must be", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetUserAccountState_WithTooManyUsers_ChangesNothing()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetUserAccountStateTool(), services, new()
        {
            ["userIds"] = Enumerable.Range(1, 21).Select(index => $"user-{index}").ToArray(),
            ["action"] = "disable",
        });

        // Assert
        Assert.StartsWith("Nothing was changed: one call acts on at most 20 users.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteUser_DeletesThroughTheUserManager()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["userName"] = "Jane",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("jane", json.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal("user-jane", Assert.Single(harness.Store.Deleted));
        Assert.DoesNotContain(harness.Jane, harness.Store.Users);
    }

    [Fact]
    public async Task DeleteUser_WhenTheUserNameDoesNotMatchTheId_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["userName"] = "sam",
        });

        // Assert
        Assert.StartsWith("Nothing was deleted: userName does not match the user with this ID", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData(AgentToolTestContext.UserId, AgentToolTestContext.UserName, "your own account")]
    [InlineData(UserRoleToolHarness.OwnerId, "owner", "site owner")]
    [InlineData("user-root", "root", "only enabled administrator")]
    public async Task DeleteUser_ForTheCallerTheSiteOwnerOrTheOnlyEnabledAdministrator_Refuses(string userId, string userName, string reason)
    {
        // Arrange
        var harness = new UserRoleToolHarness();
        var users = harness.Store.Users.Count;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUserTool(), services, new()
        {
            ["userId"] = userId,
            ["userName"] = userName,
        });

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        Assert.Contains(reason, result, StringComparison.Ordinal);
        Assert.Equal(users, harness.Store.Users.Count);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteUser_WhenTheStoreRefuses_ReportsTheError()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Store.Failure = new IdentityError { Code = "Failed", Description = "The user is referenced by an order." };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUserTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["userName"] = "jane",
        });

        // Assert
        Assert.Equal("Nothing was deleted: The user is referenced by an order.", result);
        Assert.Contains(harness.Jane, harness.Store.Users);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetUserSignInFactors_TwoFactor_SwitchesItOffAndSaysWhetherTheUserMustEnrollAgain(bool required)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            RequireTwoFactor = required,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["factors"] = new[] { ResetUserSignInFactorsTool.TwoFactorFactor },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("twoFactorDisabled").GetBoolean());
        Assert.False(json.GetProperty("authenticatorAppReset").GetBoolean());
        Assert.Equal(required, json.GetProperty("mustEnrollAgain").GetBoolean());
        Assert.False(harness.Jane.TwoFactorEnabled);

        // The authenticator key and the recovery codes are left alone and are not returned.
        Assert.Contains(harness.Jane.UserTokens, token => token.Name == FakeUserStore.AuthenticatorKeyName && token.Value == $"{UserRoleToolHarness.Canary}-AUTHENTICATOR-KEY");
        Assert.Equal(2, harness.Jane.UserTokens.Count);
        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetUserSignInFactors_TwoFactorAndAuthenticatorApp_ForgetsTheOldKeyWithoutReturningIt()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["factors"] = new[] { ResetUserSignInFactorsTool.TwoFactorFactor, ResetUserSignInFactorsTool.AuthenticatorAppFactor },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var key = Assert.Single(harness.Jane.UserTokens, token => token.Name == FakeUserStore.AuthenticatorKeyName);

        Assert.True(json.GetProperty("twoFactorDisabled").GetBoolean());
        Assert.True(json.GetProperty("authenticatorAppReset").GetBoolean());
        Assert.False(harness.Jane.TwoFactorEnabled);
        Assert.False(string.IsNullOrEmpty(key.Value));
        Assert.DoesNotContain(UserRoleToolHarness.Canary, key.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(key.Value, result, StringComparison.Ordinal);
        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetUserSignInFactors_AuthenticatorAppAloneWhileTwoFactorIsOn_RefusesSoTheUserIsNotLockedOut()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["factors"] = new[] { ResetUserSignInFactorsTool.AuthenticatorAppFactor },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: two-factor authentication is on for this user", result, StringComparison.Ordinal);
        Assert.True(harness.Jane.TwoFactorEnabled);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task ResetUserSignInFactors_Passkeys_SaysTheSiteHasNone()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["factors"] = new[] { "passkeys" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: passkeys cannot be removed here", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task ResetUserSignInFactors_OnTheCallersOwnAccount_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        harness.Caller.TwoFactorEnabled = true;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = AgentToolTestContext.UserId,
            ["factors"] = new[] { ResetUserSignInFactorsTool.TwoFactorFactor },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: this is your own account", result, StringComparison.Ordinal);
        Assert.True(harness.Caller.TwoFactorEnabled);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task ResetUserSignInFactors_ForAUserWithoutTwoFactor_ChangesNothing()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-sam",
            ["factors"] = new[] { ResetUserSignInFactorsTool.TwoFactorFactor },
        });

        // Assert
        Assert.False(AgentToolTestContext.ReadJson(result).GetProperty("changed").GetBoolean());
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task ResetUserSignInFactors_ForAUserThatDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ResetUserSignInFactorsTool(), services, new()
        {
            ["userId"] = "user-missing",
            ["factors"] = new[] { ResetUserSignInFactorsTool.TwoFactorFactor },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: there is no user with the ID 'user-missing'", result, StringComparison.Ordinal);
    }
}

using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tests.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every user and role tool acts for the person in the chat: it refuses a caller who is not signed in and a caller
/// who does not hold the permission the matching admin action asks for, with the resource that action passes, and it
/// changes nothing when it refuses. No tool returns a credential. Each JSON schema takes no argument it does not
/// declare, describes each argument and has no argument for a password, a hash or a token.
/// </summary>
public sealed class OrchardUserRoleToolCallerTests
{
    public static TheoryData<string> ToolNames => [.. UserRoleToolCalls.ToolNames];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        harness.VerifyNothingChanged();
    }

    // The resource is "none" for a check without a resource, "blank" for the blank user the Users admin page passes
    // when there is no user yet, and "jane" for the user the tool acts on.
    [Theory]
    [InlineData(ListUsersTool.TheName, "ListUsers", "blank")]
    [InlineData(GetUserTool.TheName, "View Users", "jane")]
    [InlineData(SaveUserTool.TheName, "EditUsers", "jane")]
    [InlineData(SetUserRolesTool.TheName, "EditUsers", "jane")]
    [InlineData(SetUserAccountStateTool.TheName, "EditUsers", "blank")]
    [InlineData(DeleteUserTool.TheName, "DeleteUsers", "jane")]
    [InlineData(ResetUserSignInFactorsTool.TheName, "DisableTwoFactorAuthenticationForUsers", "none")]
    [InlineData(ExplainUserAccessTool.TheName, "ViewRoles", "none")]
    [InlineData(ListRolesTool.TheName, "ViewRoles", "none")]
    [InlineData(GetRoleDetailsTool.TheName, "ViewRoles", "none")]
    [InlineData(ListPermissionsTool.TheName, "ViewRoles", "none")]
    [InlineData(SaveRoleTool.TheName, "ManageRoles", "none")]
    [InlineData(DeleteRoleTool.TheName, "ManageRoles", "none")]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminAction_RefusesAndChangesNothing(string toolName, string permission, string resource)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);

        switch (resource)
        {
            case "none":
                Assert.Null(check.Resource);
                break;
            case "blank":
                Assert.Null(Assert.IsType<User>(check.Resource).UserId);
                break;
            default:
                Assert.Same(harness.Jane, check.Resource);
                break;
        }

        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData(GetUserTool.TheName, "View Users")]
    [InlineData(SaveUserTool.TheName, "EditUsers")]
    [InlineData(SetUserRolesTool.TheName, "EditUsers")]
    [InlineData(DeleteUserTool.TheName, "DeleteUsers")]
    public async Task InvokeAsync_ForAnIdThatMatchesNoUser_GivesACallerWithoutThePermissionTheSameRefusal(string toolName, string permission)
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(toolName);

        arguments["userId"] = "user-missing";

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        // A caller who may act on no user cannot use the tool to learn which accounts exist.
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);
        Assert.Null(Assert.IsType<User>(check.Resource).UserId);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenEverySensitiveFieldOfTheUserIsSet_ReturnsNoneOfThem(string toolName)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        // The fixture is what the test says it is: every field a tool must not return holds a canary.
        Assert.StartsWith(UserRoleToolHarness.Canary, harness.Jane.PasswordHash, StringComparison.Ordinal);
        Assert.StartsWith(UserRoleToolHarness.Canary, harness.Jane.SecurityStamp, StringComparison.Ordinal);
        Assert.StartsWith(UserRoleToolHarness.Canary, harness.Jane.ResetToken, StringComparison.Ordinal);
        Assert.All(harness.Jane.UserTokens, token => Assert.StartsWith(UserRoleToolHarness.Canary, token.Value, StringComparison.Ordinal));
        Assert.All(harness.Jane.LoginInfos, login => Assert.StartsWith(UserRoleToolHarness.Canary, login.ProviderKey, StringComparison.Ordinal));
        Assert.All(harness.Jane.UserClaims, claim => Assert.StartsWith(UserRoleToolHarness.Canary, claim.ClaimValue, StringComparison.Ordinal));

        using var services = harness.Build();

        var (tool, arguments) = UserRoleToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.NotEqual(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase);

        // The call ran: the result is the JSON of a tool that did its work, not a refusal that names nothing.
        Assert.Equal(JsonValueKind.Object, AgentToolTestContext.ReadJson(result).ValueKind);
    }

    [Theory]
    [InlineData("userId", "user-jane")]
    [InlineData("userName", "jane")]
    [InlineData("email", "jane@contoso.com")]
    public async Task GetUser_HoweverTheUserIsFound_ReturnsTheAllowListedFieldsOnly(string argument, string value)
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetUserTool(), services, new()
        {
            [argument] = value,
        });

        // Assert
        var user = AgentToolTestContext.ReadJson(result).GetProperty("user");

        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase);

        string[] expected =
        [
            "culture",
            "customSettings",
            "email",
            "emailConfirmed",
            "enabled",
            "externalLoginProviders",
            "failedSignInCount",
            "hasPassword",
            "isSiteOwner",
            "lockedOut",
            "lockedOutUntil",
            "phoneNumber",
            "phoneNumberConfirmed",
            "roles",
            "timeZone",
            "timeZoneId",
            "twoFactorEnabled",
            "userId",
            "userName",
        ];

        Assert.Equal(expected, user.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsDescribesEachOneAndTakesNoCredential(string toolName)
    {
        // Arrange
        var (tool, _) = UserRoleToolCalls.Create(toolName);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.Array, schema.GetProperty("required").ValueKind);

        var properties = schema.GetProperty("properties").EnumerateObject().ToList();

        Assert.All(properties, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
        Assert.All(schema.GetProperty("required").EnumerateArray(), required => Assert.Contains(properties, property => property.Name == required.GetString()));

        // Anything a model sends has been through the AI provider and the chat history, so no tool takes a credential.
        Assert.All(properties, property =>
        {
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stamp", property.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}

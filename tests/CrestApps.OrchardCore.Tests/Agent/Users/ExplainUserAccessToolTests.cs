using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// explainUserAccess asks the authorization service with the user's own principal, names the user's roles that hold the
/// permission and the roles of the site that would grant it, reports a disabled or locked-out account, and returns none
/// of the user's claims.
/// </summary>
public sealed class ExplainUserAccessToolTests
{
    [Fact]
    public async Task ExplainUserAccess_WhenARoleOfTheUserHoldsThePermission_SaysWhichOne()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ExplainUserAccessTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["permission"] = "editcontent",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("allowed").GetBoolean());
        Assert.Equal("EditContent", json.GetProperty("permission").GetProperty("name").GetString());
        Assert.Equal("Editor", Assert.Single(json.GetProperty("rolesOfUserThatGrantIt").EnumerateArray()).GetString());
        Assert.Contains("Administrator", json.GetProperty("rolesThatGrantIt").EnumerateArray().Select(role => role.GetString()));
        Assert.Contains("through the role Editor", json.GetProperty("explanation").GetString(), StringComparison.Ordinal);

        // Jane is locked out until 2099; her claims, security stamp and tokens are never returned.
        Assert.False(string.IsNullOrEmpty(json.GetProperty("lockedOutUntil").GetString()));
        Assert.DoesNotContain(UserRoleToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainUserAccess_WhenNoRoleOfTheUserHoldsThePermission_NamesTheRolesThatWould()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            // The caller may see users and roles; the site refuses Jane the permission.
            Authorize = (permission, _) => permission.Name != "ManageUsers",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ExplainUserAccessTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["permission"] = "ManageUsers",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var granting = json.GetProperty("rolesThatGrantIt").EnumerateArray().Select(role => role.GetString()).ToList();

        Assert.False(json.GetProperty("allowed").GetBoolean());
        Assert.Empty(json.GetProperty("rolesOfUserThatGrantIt").EnumerateArray());
        Assert.Contains("Moderator", granting);
        Assert.Contains("Administrator", granting);
        Assert.DoesNotContain("Editor", granting);
        Assert.StartsWith("Not allowed: none of the user's roles holds this permission.", json.GetProperty("explanation").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplainUserAccess_ForADisabledUser_SaysTheyCannotSignIn()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ExplainUserAccessTool(), services, new()
        {
            ["userId"] = "user-sam",
            ["permission"] = "ViewContent",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("accountEnabled").GetBoolean());
        Assert.Contains("The account is disabled", json.GetProperty("explanation").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplainUserAccess_ForAnUnknownPermission_SuggestsTheClosestNames()
    {
        // Arrange
        var harness = new UserRoleToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ExplainUserAccessTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["permission"] = "EditContnt",
        });

        // Assert
        Assert.StartsWith("There is no permission named 'EditContnt' on this site. Did you mean EditContent", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplainUserAccess_WhenTheCallerMayNotSeeTheUser_Refuses()
    {
        // Arrange
        var harness = new UserRoleToolHarness
        {
            Authorize = (permission, _) => permission.Name != "View Users",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ExplainUserAccessTool(), services, new()
        {
            ["userId"] = "user-jane",
            ["permission"] = "EditContent",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == "View Users");
    }
}

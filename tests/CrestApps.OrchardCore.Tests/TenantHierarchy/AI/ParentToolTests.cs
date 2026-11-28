using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.TenantHierarchy.AI;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy.AI;

/// <summary>
/// The child tenant tools run on the real child tenant and access grant managers over a SQLite registry: they apply the
/// policy's address rules and recipes, the confirmations of the child tenants page, and queue the setup of a new child
/// tenant so that it runs when the chat's scope ends.
/// </summary>
public sealed class ParentToolTests
{
    [Fact]
    public async Task ListChildTenants_FiltersByState_AndReturnsThePolicyRecipesOnly()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        await harness.AddChildAsync("south", state: ChildTenantRuntimeState.Suspended);
        var services = harness.Build(authorize: (permission, _) => permission.Name != "EnterChildTenants");

        // Act
        var result = await InvokeAsync(new ListChildTenantsTool(), services, new() { ["state"] = "Suspended", ["includePolicy"] = true });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        var policy = json.GetProperty("policy");

        Assert.Equal("entry-south", item.GetProperty("child").GetProperty("entryId").GetString());
        Assert.Equal("Suspended", item.GetProperty("child").GetProperty("state").GetString());
        Assert.False(item.GetProperty("canEnter").GetBoolean());
        Assert.Equal(2, json.GetProperty("totalChildren").GetInt32());
        Assert.Equal(5, policy.GetProperty("maxChildren").GetInt32());
        Assert.Equal("Retail", Assert.Single(policy.GetProperty("setupRecipes").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task SaveChildTenant_CreatesTheChild_AndItsSetupRunsWhenTheChatScopeEnds()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var result = await ParentToolHarness.InShellScopeAsync(services, scopeServices => InvokeAsync(new SaveChildTenantTool(), scopeServices, new()
        {
            ["displayName"] = "Bella's Bakery",
            ["slug"] = "bellas-bakery",
        }));
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var entryId = json.GetProperty("entryId").GetString();
        var entry = await harness.EntryStore.FindByEntryIdAsync(entryId);

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.True(json.GetProperty("setupQueued").GetBoolean());
        Assert.Equal("bellas-bakery.firm.platform.com", entry.Host);
        Assert.Equal("Retail", entry.RecipeName);
        Assert.Equal(new[] { $"create:{entryId}", $"setup:{entryId}" }, harness.BrokerCalls);
        Assert.Equal(ChildTenantStatus.Ready, entry.Status);
        Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-canary", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChildTenant_CreatesNothing_WhenTheSetupCannotBeQueued()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new SaveChildTenantTool(), services, new()
        {
            ["displayName"] = "Bella's Bakery",
            ["slug"] = "bellas-bakery",
        });

        // Assert
        Assert.StartsWith("Nothing was created", result, StringComparison.Ordinal);
        Assert.Empty(harness.BrokerCalls);
        Assert.Equal(0, await harness.EntryStore.CountAsync());
    }

    [Theory]
    [InlineData("Bad_Slug!", "Retail", "slug")]
    [InlineData("taken", "Retail", "slug")]
    [InlineData("bellas", "Blog", "recipeName")]
    [InlineData("bellas", "Unknown", "recipeName")]
    public async Task SaveChildTenant_AppliesTheAddressAndRecipeRulesOfThePolicy(string slug, string recipeName, string field)
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var result = await ParentToolHarness.InShellScopeAsync(services, scopeServices => InvokeAsync(new SaveChildTenantTool(), scopeServices, new()
        {
            ["displayName"] = "Bella's Bakery",
            ["slug"] = slug,
            ["recipeName"] = recipeName,
        }));
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetProperty("field").GetString() == field);
        Assert.Empty(harness.BrokerCalls);
        Assert.Equal(0, await harness.EntryStore.CountAsync());
    }

    [Fact]
    public async Task SaveChildTenant_RefusesAChildBeyondThePolicyLimit()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync(policy => policy.MaxChildren = 1);
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var result = await ParentToolHarness.InShellScopeAsync(services, scopeServices => InvokeAsync(new SaveChildTenantTool(), scopeServices, new()
        {
            ["displayName"] = "South",
            ["slug"] = "south",
        }));

        // Assert
        Assert.StartsWith("The child tenant was not created", result, StringComparison.Ordinal);
        Assert.Contains("limit of 1", result, StringComparison.Ordinal);
        Assert.Equal(1, await harness.EntryStore.CountAsync());
    }

    [Fact]
    public async Task SaveChildTenant_UpdatesTheNameAndAddress_ButNeverTheRecipe()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var refused = await InvokeAsync(new SaveChildTenantTool(), services, new() { ["entryId"] = "entry-north", ["recipeName"] = "Blog" });
        var result = await InvokeAsync(new SaveChildTenantTool(), services, new() { ["entryId"] = "entry-north", ["displayName"] = "North Store", ["slug"] = "north-store" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var entry = await harness.EntryStore.FindByEntryIdAsync("entry-north");

        Assert.Contains("cannot change", refused, StringComparison.Ordinal);
        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("addressChanged").GetBoolean());
        Assert.Equal("North Store", entry.DisplayName);
        Assert.Equal("north-store.firm.platform.com", entry.Host);
        Assert.Equal("Retail", entry.RecipeName);
        Assert.Contains("update:entry-north", harness.BrokerCalls);
    }

    [Fact]
    public async Task ChangeChildTenantState_SuspendsAndReportsEachChild()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new ChangeChildTenantStateTool(), services, new()
        {
            ["entryIds"] = new[] { "entry-north", "entry-missing" },
            ["action"] = "Suspend",
        }));

        // Assert
        var results = json.GetProperty("results").EnumerateArray().ToList();

        Assert.Equal(1, json.GetProperty("changedCount").GetInt32());
        Assert.True(results[0].GetProperty("changed").GetBoolean());
        Assert.False(results[1].GetProperty("changed").GetBoolean());
        Assert.Equal(ChildTenantRuntimeState.Suspended, harness.States["entry-north"]);
    }

    [Fact]
    public async Task RemoveChildTenant_NeedsTheExactNameAndASuspendedChild()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        await harness.AddChildAsync("south", state: ChildTenantRuntimeState.Suspended);
        var services = harness.Build();

        // Act
        var wrongName = await InvokeAsync(new RemoveChildTenantTool(), services, new() { ["entryId"] = "entry-south", ["confirmName"] = "south" });
        var running = await InvokeAsync(new RemoveChildTenantTool(), services, new() { ["entryId"] = "entry-north", ["confirmName"] = "North" });
        var removed = AgentToolTestContext.ReadJson(await InvokeAsync(new RemoveChildTenantTool(), services, new() { ["entryId"] = "entry-south", ["confirmName"] = "South" }));

        // Assert
        Assert.StartsWith("Nothing was removed", wrongName, StringComparison.Ordinal);
        Assert.Contains("Suspend the", running, StringComparison.Ordinal);
        Assert.True(removed.GetProperty("removed").GetBoolean());
        Assert.Contains("remove:entry-south", harness.BrokerCalls);
        Assert.DoesNotContain("remove:entry-north", harness.BrokerCalls);
        Assert.Null(await harness.EntryStore.FindByEntryIdAsync("entry-south"));
    }

    [Fact]
    public async Task RemoveChildTenant_WithAGracePeriod_SchedulesTheRemoval_AndRestoreCancelsIt()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync(policy => policy.RemovalGraceDays = 7);
        await harness.AddChildAsync("south", state: ChildTenantRuntimeState.Suspended);
        var services = harness.Build();

        // Act
        var removal = AgentToolTestContext.ReadJson(await InvokeAsync(new RemoveChildTenantTool(), services, new() { ["entryId"] = "entry-south", ["confirmName"] = "South" }));
        var pendingStatus = (await harness.EntryStore.FindByEntryIdAsync("entry-south")).Status;
        var restore = AgentToolTestContext.ReadJson(await InvokeAsync(new RestoreChildTenantTool(), services, new() { ["entryId"] = "entry-south" }));

        // Assert
        Assert.True(removal.GetProperty("removalScheduled").GetBoolean());
        Assert.Equal("2026-11-25T12:00:00+00:00", removal.GetProperty("removalDate").GetString());
        Assert.Equal(ChildTenantStatus.PendingRemoval, pendingStatus);
        Assert.True(restore.GetProperty("restored").GetBoolean());
        Assert.Equal(ChildTenantStatus.Ready, (await harness.EntryStore.FindByEntryIdAsync("entry-south")).Status);
        Assert.DoesNotContain("remove:entry-south", harness.BrokerCalls);
    }

    [Fact]
    public async Task RestoreChildTenant_RefusesAChildThatIsNotPendingRemoval()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new RestoreChildTenantTool(), services, new() { ["entryId"] = "entry-north" });

        // Assert
        Assert.Contains("only a child tenant pending removal", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryChildTenantSetup_CreatesAFailedChildAgain_AndRunsItsSetup()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north", ChildTenantStatus.Failed, ChildTenantRuntimeState.Uninitialized);
        var services = harness.Build();

        // Act
        var result = await ParentToolHarness.InShellScopeAsync(services, scopeServices => InvokeAsync(new RetryChildTenantSetupTool(), scopeServices, new() { ["entryId"] = "entry-north" }));
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var entryId = json.GetProperty("entryId").GetString();

        Assert.NotEqual("entry-north", entryId);
        Assert.Equal(new[] { "remove:entry-north", $"create:{entryId}", $"setup:{entryId}" }, harness.BrokerCalls);
        Assert.Equal("north", (await harness.EntryStore.FindByEntryIdAsync(entryId)).Slug);
    }

    [Fact]
    public async Task RetryChildTenantSetup_RefusesAChildThatDidNotFail()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var result = await ParentToolHarness.InShellScopeAsync(services, scopeServices => InvokeAsync(new RetryChildTenantSetupTool(), scopeServices, new() { ["entryId"] = "entry-north" }));

        // Assert
        Assert.Contains("not in the Failed state", result, StringComparison.Ordinal);
        Assert.Empty(harness.BrokerCalls);
    }

    [Fact]
    public async Task DiscardChildTenant_DiscardsAFailedSetup_WithCreateChildTenants()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north", ChildTenantStatus.Failed, ChildTenantRuntimeState.Uninitialized);
        var services = harness.Build(authorize: (permission, _) => permission.Name == "CreateChildTenants");

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new DiscardChildTenantTool(), services, new() { ["entryId"] = "entry-north" }));

        // Assert
        Assert.True(json.GetProperty("discarded").GetBoolean());
        Assert.Null(await harness.EntryStore.FindByEntryIdAsync("entry-north"));
    }

    [Fact]
    public async Task DiscardChildTenant_DismissesAChildThePlatformChanged_OnlyWithManageChildTenants()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north", state: ChildTenantRuntimeState.ChangedByPlatform);
        var createOnly = harness.Build(authorize: (permission, _) => permission.Name == "CreateChildTenants");
        var manager = harness.Build(authorize: (permission, _) => permission.Name == "ManageChildTenants");

        // Act
        var refused = await InvokeAsync(new DiscardChildTenantTool(), createOnly, new() { ["entryId"] = "entry-north" });
        var dismissed = AgentToolTestContext.ReadJson(await InvokeAsync(new DiscardChildTenantTool(), manager, new() { ["entryId"] = "entry-north" }));

        // Assert
        Assert.Equal(TenantHierarchyAIToolBase.NotAllowedMessage, refused);
        Assert.True(dismissed.GetProperty("dismissed").GetBoolean());
        Assert.Empty(harness.BrokerCalls);
    }

    [Fact]
    public async Task DiscardChildTenant_RefusesAWorkingChild()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new DiscardChildTenantTool(), services, new() { ["entryId"] = "entry-north" });

        // Assert
        Assert.Contains("neither a failed setup", result, StringComparison.Ordinal);
        Assert.NotNull(await harness.EntryStore.FindByEntryIdAsync("entry-north"));
    }

    [Fact]
    public async Task ChildTenantFeatures_AreListed_AndOnlyOfferedFeaturesChange()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        harness.Features.Add(new ChildFeatureInfo { Id = "OrchardCore.Forms", Name = "Forms", Category = "Content" });
        harness.Features.Add(new ChildFeatureInfo { Id = "OrchardCore.Media", Name = "Media", Category = "Content", IsEnabled = true });
        var services = harness.Build();

        // Act
        var list = AgentToolTestContext.ReadJson(await InvokeAsync(new ListChildTenantFeaturesTool(), services, new() { ["entryId"] = "entry-north" }));
        var set = AgentToolTestContext.ReadJson(await InvokeAsync(new SetChildTenantFeaturesTool(), services, new()
        {
            ["entryId"] = "entry-north",
            ["featureIds"] = new[] { "OrchardCore.Forms", "CrestApps.OrchardCore.AI.Mcp.Local", "OrchardCore.Media" },
            ["enable"] = true,
        }));

        // Assert
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal("OrchardCore.Forms", Assert.Single(set.GetProperty("enabled").EnumerateArray()).GetString());
        Assert.Equal(2, set.GetProperty("ignored").GetArrayLength());
        Assert.Equal(new[] { "enable:entry-north:OrchardCore.Forms" }, harness.BrokerCalls);
    }

    [Fact]
    public async Task ChildTenantFeatures_AreNotListedForASuspendedChild()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north", state: ChildTenantRuntimeState.Suspended);
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new ListChildTenantFeaturesTool(), services, new() { ["entryId"] = "entry-north" });

        // Assert
        Assert.Contains("is not running", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccessGrants_AreGrantedListedReplacedAndRevoked()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        await harness.AddChildAsync("north");
        var services = harness.Build();

        // Act
        var granted = AgentToolTestContext.ReadJson(await InvokeAsync(new GrantChildTenantAccessTool(), services, new()
        {
            ["principalType"] = "Role",
            ["principal"] = "support",
            ["entryId"] = "entry-north",
            ["childRoles"] = new[] { "editor" },
        }));
        var replaced = AgentToolTestContext.ReadJson(await InvokeAsync(new GrantChildTenantAccessTool(), services, new()
        {
            ["principalType"] = "Role",
            ["principal"] = "Support",
            ["entryId"] = "entry-north",
            ["childRoles"] = new[] { "Author" },
        }));
        var list = AgentToolTestContext.ReadJson(await InvokeAsync(new ListChildTenantAccessGrantsTool(), services, new() { ["entryId"] = "entry-north" }));
        var grantId = Assert.Single(list.GetProperty("grants").EnumerateArray()).GetProperty("grantId").GetString();
        var revoked = AgentToolTestContext.ReadJson(await InvokeAsync(new RevokeChildTenantAccessTool(), services, new() { ["grantId"] = grantId }));

        // Assert
        Assert.False(granted.GetProperty("replacedExistingGrant").GetBoolean());
        Assert.Equal("Editor", Assert.Single(granted.GetProperty("grant").GetProperty("childRoles").EnumerateArray()).GetString());
        Assert.True(replaced.GetProperty("replacedExistingGrant").GetBoolean());
        Assert.Equal("Editor", Assert.Single(replaced.GetProperty("previousChildRoles").EnumerateArray()).GetString());
        Assert.Equal("Administrator", Assert.Single(list.GetProperty("inheritedGrants").EnumerateArray()).GetProperty("principal").GetString());
        Assert.DoesNotContain(list.GetProperty("parentRoles").EnumerateArray(), role => role.GetString() == "Anonymous");
        Assert.True(revoked.GetProperty("revoked").GetBoolean());
        Assert.Empty(await services.GetRequiredService<AccessGrantStore>().ListForChildAsync("entry-north"));
    }

    [Theory]
    [InlineData("Role", "Support", "Owner", "is not a role of")]
    [InlineData("Role", "Nobody", "Editor", "role was not found")]
    [InlineData("User", "someone@example.com", "Editor", "No user")]
    public async Task GrantChildTenantAccess_RefusesWhatTheAccessScreenWouldNotOffer(string principalType, string principal, string childRole, string expected)
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new GrantChildTenantAccessTool(), services, new()
        {
            ["principalType"] = principalType,
            ["principal"] = principal,
            ["childRoles"] = new[] { childRole },
        });

        // Assert
        Assert.StartsWith("Nothing was granted", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrantChildTenantAccess_GrantsAParentUserByEmail()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new GrantChildTenantAccessTool(), services, new()
        {
            ["principalType"] = "User",
            ["principal"] = "ana@example.com",
            ["childRoles"] = new[] { "Editor" },
        }));

        // Assert
        Assert.Equal("ana", json.GetProperty("grant").GetProperty("principal").GetString());
        Assert.Equal("Every child tenant", json.GetProperty("appliesTo").GetString());
    }

    [Fact]
    public async Task RevokeChildTenantAccess_WithAnUnknownGrant_SaysSo()
    {
        // Arrange
        await using var harness = await ParentToolHarness.CreateAsync();
        var services = harness.Build();

        // Act
        var result = await InvokeAsync(new RevokeChildTenantAccessTool(), services, new() { ["grantId"] = "missing" });

        // Assert
        Assert.Contains("No access grant has the id 'missing'", result, StringComparison.Ordinal);
    }

    private static Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(tool, services, arguments);
}

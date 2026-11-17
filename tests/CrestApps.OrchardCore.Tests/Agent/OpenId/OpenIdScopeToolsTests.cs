using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.Tests.Agent.Security;

namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// The OpenID scope tools list, save and delete through the scope manager, with the checks of the Scopes admin page:
/// a name no other scope has, and no resource that names the tenant itself.
/// </summary>
public sealed class OpenIdScopeToolsTests
{
    [Fact]
    public async Task ListOpenIdScopes_ReturnsNameDisplayNameDescriptionAndResources()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListOpenIdScopesTool(), services));

        // Assert
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var orders = items[1];

        Assert.Equal(2, result.GetProperty("total").GetInt32());
        Assert.Equal(["email", "orders.read"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("scope-orders", orders.GetProperty("id").GetString());
        Assert.Equal("Read orders", orders.GetProperty("displayName").GetString());
        Assert.Equal("Read access to the orders API.", orders.GetProperty("description").GetString());
        Assert.Equal(["orders-api"], Strings(orders, "resources"));
    }

    [Fact]
    public async Task ListOpenIdScopes_WithSearch_KeepsTheScopesWhoseNameOrDisplayNameContainsTheText()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListOpenIdScopesTool(), services, new()
        {
            ["search"] = "ORDERS",
        }));

        // Assert
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal("orders.read", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task SaveOpenIdScope_WithoutAnId_CreatesTheScope()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["name"] = "invoices.write",
            ["displayName"] = "Write invoices",
            ["description"] = "Write access to the invoices API.",
            ["resources"] = new[] { "invoices-api", "billing-api" },
        }));

        // Assert
        var created = Assert.Single(harness.OpenId.Scopes, scope => scope.Name == "invoices.write");

        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.Equal(created.Id, result.GetProperty("scope").GetProperty("id").GetString());
        Assert.Equal("Write invoices", created.DisplayName);
        Assert.Equal("Write access to the invoices API.", created.Description);
        Assert.Equal(["billing-api", "invoices-api"], created.Resources.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SaveOpenIdScope_WithAnId_ChangesTheGivenFieldsAndKeepsTheOthers()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["id"] = "scope-orders",
            ["description"] = "Reads the orders.",
        }));

        // Assert
        var scope = Assert.Single(harness.OpenId.Scopes, stored => stored.Id == "scope-orders");

        Assert.False(result.GetProperty("created").GetBoolean());
        Assert.Equal("Reads the orders.", scope.Description);
        Assert.Equal("orders.read", scope.Name);
        Assert.Equal("Read orders", scope.DisplayName);
        Assert.Equal(["orders-api"], scope.Resources);
    }

    [Fact]
    public async Task SaveOpenIdScope_WithANameAnotherScopeHas_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var create = await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["name"] = "email",
            ["displayName"] = "Email again",
        });

        var rename = await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["id"] = "scope-orders",
            ["name"] = "email",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the name 'email' is taken by the scope with the id 'scope-email'.", create, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: the name 'email' is taken by the scope with the id 'scope-email'.", rename, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdScope_WithTheTenantItselfAsAResource_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["id"] = "scope-orders",
            ["resources"] = new[] { "orders-api", "oct:Default" },
        });

        // Assert
        Assert.Equal("Nothing was saved: the resources cannot contain 'oct:Default'.", result);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdScope_WhenTheScopeManagerRefusesTheScope_ReturnsItsReason()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["name"] = "two words",
            ["displayName"] = "Two words",
        });

        // Assert
        Assert.Equal("Nothing was saved: The scope name cannot contain spaces.", result);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdScope_ForANewScopeWithoutADisplayName_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdScopeTool(), services, new()
        {
            ["name"] = "invoices.write",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: give name and displayName for a new scope", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteOpenIdScope_DeletesTheScopeAndNamesTheApplicationsThatWereAllowedIt()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteOpenIdScopeTool(), services, new()
        {
            ["id"] = "scope-orders",
            ["name"] = "orders.read",
        }));

        // Assert
        Assert.True(result.GetProperty("deleted").GetBoolean());
        Assert.Equal(["backend-service"], Strings(result, "applicationsThatWereAllowedIt"));
        Assert.DoesNotContain(harness.OpenId.Scopes, scope => scope.Id == "scope-orders");
    }

    [Theory]
    [InlineData("scope-orders", "email")]
    [InlineData("scope-missing", "orders.read")]
    public async Task DeleteOpenIdScope_WhenTheIdAndTheNameDoNotNameOneScope_DeletesNothing(string id, string name)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOpenIdScopeTool(), services, new()
        {
            ["id"] = id,
            ["name"] = name,
        });

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    private static List<string> Strings(JsonElement element, string property)
        => element.GetProperty(property).EnumerateArray().Select(value => value.GetString()).ToList();
}

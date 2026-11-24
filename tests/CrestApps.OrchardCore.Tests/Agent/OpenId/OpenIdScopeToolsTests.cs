using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.Tests.Agent.Security;

namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// The OpenID scope tool lists the scopes through the scope manager, as the Scopes admin page does.
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

    private static List<string> Strings(JsonElement element, string property)
        => element.GetProperty(property).EnumerateArray().Select(value => value.GetString()).ToList();
}

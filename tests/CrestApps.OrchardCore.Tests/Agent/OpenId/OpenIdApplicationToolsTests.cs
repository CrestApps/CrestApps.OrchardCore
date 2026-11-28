using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.Tests.Agent.Security;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// The OpenID application tools list and read through the application manager and never return a client secret.
/// </summary>
public sealed class OpenIdApplicationToolsTests
{
    [Fact]
    public async Task ListOpenIdApplications_ReturnsIdClientIdNameAndType()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListOpenIdApplicationsTool(), services));

        // Assert
        var items = result.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, result.GetProperty("total").GetInt32());
        Assert.Equal(["Backend service", "Portal"], items.Select(item => item.GetProperty("displayName").GetString()));
        Assert.Equal(["app-backend", "app-portal"], items.Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(["backend-service", "portal-web"], items.Select(item => item.GetProperty("clientId").GetString()));
        Assert.Equal(["confidential", "public"], items.Select(item => item.GetProperty("type").GetString()));
        Assert.All(items, item => Assert.Equal(["clientId", "displayName", "id", "type"], item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task ListOpenIdApplications_WithSearch_KeepsTheApplicationsWhoseDisplayNameContainsTheText()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListOpenIdApplicationsTool(), services, new()
        {
            ["search"] = "port",
        }));

        // Assert
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal("portal-web", item.GetProperty("clientId").GetString());
        Assert.Equal(1, result.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GetOpenIdApplication_ByClientId_ReturnsWhatTheEditPageShows()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetOpenIdApplicationTool(), services, new()
        {
            ["clientId"] = "portal-web",
        }));

        // Assert
        var application = result.GetProperty("application");

        Assert.Equal("app-portal", application.GetProperty("id").GetString());
        Assert.Equal("public", application.GetProperty("type").GetString());
        Assert.False(application.GetProperty("hasClientSecret").GetBoolean());
        Assert.Equal("explicit", application.GetProperty("consentType").GetString());
        Assert.Equal(["authorizationCode", "refreshToken"], Strings(application, "allowedFlows"));
        Assert.Equal(["logout"], Strings(application, "allowedEndpoints"));
        Assert.True(application.GetProperty("requirePkce").GetBoolean());
        Assert.False(application.GetProperty("requirePushedAuthorizationRequests").GetBoolean());
        Assert.Equal(["email"], Strings(application, "scopes"));
        Assert.Equal(["https://portal.contoso.com/callback", "http://localhost:5000/callback"], Strings(application, "redirectUris"));
    }

    [Fact]
    public async Task GetOpenIdApplication_ForAConfidentialClient_SaysItHasASecretAndDoesNotReturnIt()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new GetOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
        });

        // Assert
        var application = AgentToolTestContext.ReadJson(text).GetProperty("application");

        Assert.True(application.GetProperty("hasClientSecret").GetBoolean());
        Assert.Equal(["clientCredentials"], Strings(application, "allowedFlows"));
        Assert.Equal(["Editor"], Strings(application, "roles"));
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(application.EnumerateObject(), property => property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) && property.Name != "hasClientSecret");
    }

    [Theory]
    [InlineData("id", "app-missing")]
    [InlineData("clientId", "missing-client")]
    public async Task GetOpenIdApplication_ForAnApplicationThatDoesNotExist_SaysSo(string argument, string value)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetOpenIdApplicationTool(), services, new()
        {
            [argument] = value,
        });

        // Assert
        Assert.StartsWith("Nothing was found:", result, StringComparison.Ordinal);
        Assert.Contains(value, result, StringComparison.Ordinal);
    }

    private static List<string> Strings(JsonElement element, string property)
        => element.GetProperty(property).EnumerateArray().Select(value => value.GetString()).ToList();
}

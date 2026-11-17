using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.Tests.Agent.Security;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// The OpenID application tools read and delete through the application manager and save through the application
/// recipe step of the OpenID module. No tool returns, takes or writes a client secret; a confidential client cannot
/// be created; a role is given under the rules for giving it to a user; and a new redirect address must be absolute
/// https.
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

    [Fact]
    public async Task SaveOpenIdApplication_WithoutAnId_CreatesAPublicClientThroughTheApplicationStep()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["clientId"] = "spa-client",
            ["displayName"] = "Single page app",
            ["redirectUris"] = new[] { "https://spa.contoso.com/callback" },
            ["allowedFlows"] = new[] { "authorizationCode", "refreshToken" },
            ["allowedEndpoints"] = new[] { "logout" },
            ["requirePkce"] = true,
            ["scopes"] = new[] { "email" },
        }));

        // Assert
        var step = Assert.Single(harness.OpenId.Steps);
        var created = Assert.Single(harness.OpenId.Applications, application => application.ClientId == "spa-client");

        Assert.True(result.GetProperty("saved").GetBoolean());
        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.Equal("public", step["Type"].GetValue<string>());
        Assert.Equal("explicit", step["ConsentType"].GetValue<string>());
        Assert.Equal("https://spa.contoso.com/callback", step["RedirectUris"].GetValue<string>());
        Assert.True(step["AllowAuthorizationCodeFlow"].GetValue<bool>());
        Assert.True(step["AllowRefreshTokenFlow"].GetValue<bool>());
        Assert.False(step["AllowImplicitFlow"].GetValue<bool>());
        Assert.False(step["AllowPasswordFlow"].GetValue<bool>());
        Assert.True(step["RequireProofKeyForCodeExchange"].GetValue<bool>());

        // The step has a field for a client secret. The tool never writes it.
        Assert.False(step.ContainsKey("ClientSecret"));
        Assert.Null(created.ClientSecret);
        Assert.Equal(created.Id, result.GetProperty("application").GetProperty("id").GetString());
        Assert.Equal(["authorizationCode", "refreshToken"], Strings(result.GetProperty("application"), "allowedFlows"));
    }

    [Fact]
    public async Task SaveOpenIdApplication_ForANewConfidentialClient_IsRefusedAndPointsToTheAdminPage()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["clientId"] = "new-backend",
            ["displayName"] = "New backend",
            ["type"] = "confidential",
            ["allowedFlows"] = new[] { "clientCredentials" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: this tool creates public clients only.", result, StringComparison.Ordinal);
        Assert.Contains("Applications admin page", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("app-portal", "confidential")]
    [InlineData("app-backend", "public")]
    public async Task SaveOpenIdApplication_ThatChangesTheTypeOfAnApplication_IsRefused(string id, string type)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = id,
            ["type"] = type,
            ["displayName"] = "Renamed",
        });

        // Assert: public to confidential would need a secret, and confidential to public would delete the stored one.
        Assert.StartsWith("Nothing was saved: this tool does not change the type of an application.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_ForAConfidentialClient_KeepsItsSecretAndWritesNone()
    {
        // Arrange
        var harness = new SecurityToolHarness();
        var secretBefore = harness.OpenId.Backend.ClientSecret;

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
            ["displayName"] = "Backend API",
            ["allowedEndpoints"] = new[] { "introspection" },
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var step = Assert.Single(harness.OpenId.Steps);

        Assert.StartsWith(SecurityToolHarness.Canary, secretBefore, StringComparison.Ordinal);
        Assert.Equal(secretBefore, harness.OpenId.Backend.ClientSecret);
        Assert.False(step.ContainsKey("ClientSecret"));
        Assert.Equal("confidential", step["Type"].GetValue<string>());
        Assert.DoesNotContain(SecurityToolHarness.Canary, step.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Backend API", harness.OpenId.Backend.DisplayName);
        Assert.False(result.GetProperty("created").GetBoolean());
        Assert.Equal(["allowedEndpoints", "displayName"], Strings(result, "changed").Order(StringComparer.Ordinal));

        // What the call did not name is kept: the flow, the role and the scope of the application.
        Assert.True(step["AllowClientCredentialsFlow"].GetValue<bool>());
        Assert.Equal(["Editor"], harness.OpenId.Backend.Roles);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read", harness.OpenId.Backend.Permissions);
    }

    [Theory]
    [InlineData("http://portal.contoso.com/callback")]
    [InlineData("/callback")]
    [InlineData("com.contoso.app:/callback")]
    [InlineData("https://portal.contoso.com/callback#token")]
    [InlineData("https://portal.contoso.com/a b")]
    public async Task SaveOpenIdApplication_ThatAddsAnAddressThatIsNotAbsoluteHttps_IsRefused(string address)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var redirect = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-portal",
            ["redirectUris"] = new[] { "https://portal.contoso.com/callback", address },
        });

        var postLogout = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-portal",
            ["postLogoutRedirectUris"] = new[] { address },
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", redirect, StringComparison.Ordinal);
        Assert.Contains("is not an absolute https address", redirect, StringComparison.Ordinal);
        Assert.Contains("is not an absolute https address", postLogout, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_KeepsAStoredAddressAPersonEntered()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act: the http address of the fixture is already on the application; the https one is new.
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-portal",
            ["redirectUris"] = new[] { "http://localhost:5000/callback", "https://portal.contoso.com/signin" },
        }));

        // Assert
        Assert.True(result.GetProperty("saved").GetBoolean());
        Assert.Equal(["http://localhost:5000/callback", "https://portal.contoso.com/signin"], harness.OpenId.Portal.RedirectUris);
    }

    [Fact]
    public async Task SaveOpenIdApplication_ThatGivesTheAdministratorRole_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
            ["roles"] = new[] { "Editor", "Administrator" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: this tool does not give the administrator role 'Administrator' to an application", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_ThatGivesARoleTheCallerMayNotAssign_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness
        {
            Authorize = (permission, _) => permission.Name != "AssignRoleToUsers",
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
            ["roles"] = new[] { "Editor", "Author" },
        });

        // Assert: the role the application already has is not asked about; the one the call adds is, on the role.
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks, entry => entry.Permission == "AssignRoleToUsers");

        Assert.Equal("Nothing was saved: you are not allowed to assign the role 'Author'.", result);
        Assert.Equal("Author", Assert.IsAssignableFrom<IRole>(check.Resource).RoleName);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_ThatGivesARoleTheCallerMayAssign_SavesIt()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
            ["roles"] = new[] { "author" },
        }));

        // Assert: the role is stored with the name the site gives it, and the one left out is taken away.
        Assert.True(result.GetProperty("saved").GetBoolean());
        Assert.Equal(["Author"], harness.OpenId.Backend.Roles);
        Assert.Equal(["roles"], Strings(result, "changed"));
    }

    [Fact]
    public async Task SaveOpenIdApplication_ThatGivesARoleThatDoesNotExist_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-backend",
            ["roles"] = new[] { "Anonymous" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: 'Anonymous' cannot be given to an application.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("implicit")]
    [InlineData("password")]
    public async Task SaveOpenIdApplication_ThatTurnsOnADiscouragedFlow_NeedsTheConfirmation(string flow)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["id"] = "app-portal",
            ["allowedFlows"] = new[] { "authorizationCode", flow },
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, arguments);
        var stepsAfterRefusal = harness.OpenId.Steps.Count;

        arguments["confirmDiscouragedFlows"] = true;

        var confirmed = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, arguments));

        // Assert
        Assert.StartsWith($"Nothing was saved: this call turns on the {flow} flow", refused, StringComparison.Ordinal);
        Assert.Equal(0, stepsAfterRefusal);
        Assert.True(confirmed.GetProperty("saved").GetBoolean());
        Assert.Contains(flow, Strings(confirmed.GetProperty("application"), "allowedFlows"));
        Assert.Contains(Strings(confirmed, "warnings"), warning => warning.Contains("discouraged", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveOpenIdApplication_WithAScopeThatDoesNotExist_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-portal",
            ["scopes"] = new[] { "email", "invoices.write" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: there is no scope named 'invoices.write'.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_WithAClientIdThatIsTaken_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["clientId"] = "portal-web",
            ["displayName"] = "Another portal",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the client id 'portal-web' is taken by the application with the id 'app-portal'.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveOpenIdApplication_WhenTheStepOfTheOpenIdModuleIsNotThere_SaysNothingWasSaved()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.OpenId.StepIsRegistered = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOpenIdApplicationTool(), services, new()
        {
            ["clientId"] = "spa-client",
            ["displayName"] = "Single page app",
        });

        // Assert: the tool reads the application back, so it does not say "saved" for work that did not run.
        Assert.StartsWith("Nothing was saved: the OpenID application step did not run.", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteOpenIdApplication_DeletesTheApplicationTheIdAndTheClientIdName()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteOpenIdApplicationTool(), services, new()
        {
            ["id"] = "app-portal",
            ["clientId"] = "portal-web",
        }));

        // Assert
        Assert.True(result.GetProperty("deleted").GetBoolean());
        Assert.Equal("Portal", result.GetProperty("displayName").GetString());
        Assert.DoesNotContain(harness.OpenId.Applications, application => application.Id == "app-portal");
        Assert.Single(harness.OpenId.Applications);
    }

    [Theory]
    [InlineData("app-portal", "backend-service")]
    [InlineData("app-missing", "portal-web")]
    public async Task DeleteOpenIdApplication_WhenTheIdAndTheClientIdDoNotNameOneApplication_DeletesNothing(string id, string clientId)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOpenIdApplicationTool(), services, new()
        {
            ["id"] = id,
            ["clientId"] = clientId,
        });

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    private static List<string> Strings(JsonElement element, string property)
        => element.GetProperty(property).EnumerateArray().Select(value => value.GetString()).ToList();
}

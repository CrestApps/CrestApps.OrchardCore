using System.Text.Json;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using CrestApps.OrchardCore.Tests.Agent.Security;
using OrchardCore.Entities;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.Tests.Agent.RateLimits;

/// <summary>
/// The rate-limit tools work on the policy store of the Rate Limits admin page with the rules of that page: the tenant
/// reloads when a policy starts or stops applying, and a policy that protects the sign-in and account endpoints is not
/// disabled unless the call says so.
/// </summary>
public sealed class RateLimitToolsTests
{
    [Fact]
    public async Task ListRateLimitPolicies_ReturnsThePoliciesWithTheirLimitersAndTheBuiltInLimits()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRateLimitPoliciesTool(), services));

        // Assert
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var contact = items.Single(item => item.GetProperty("id").GetString() == "policy-contact");
        var limiter = Assert.Single(contact.GetProperty("limiters").EnumerateArray());

        Assert.Equal(4, result.GetProperty("total").GetInt32());
        Assert.Equal(["Contact API", "Draft policy", "Login throttle", "Whole site"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("endpoint", contact.GetProperty("scope").GetString());
        Assert.Equal("/api/contact", contact.GetProperty("path").GetString());
        Assert.True(contact.GetProperty("enabled").GetBoolean());
        Assert.Equal("fixedWindow", limiter.GetProperty("type").GetString());
        Assert.Equal("FixedWindow limiter", limiter.GetProperty("summary").GetString());
        Assert.Equal(10, limiter.GetProperty("settings").GetProperty("permitLimit").GetInt32());
        Assert.Equal(60, limiter.GetProperty("settings").GetProperty("windowSeconds").GetInt32());

        // The limits modules register in code.
        var route = Assert.Single(result.GetProperty("builtInRouteLimits").EnumerateArray());
        var group = Assert.Single(result.GetProperty("builtInGroupLimits").EnumerateArray());

        Assert.Equal("Login", route.GetProperty("routeName").GetString());
        Assert.Equal(["POST"], route.GetProperty("methods").EnumerateArray().Select(method => method.GetString()));
        Assert.Equal("password-authentication", group.GetProperty("group").GetString());
        Assert.Equal(1, group.GetProperty("limiterCount").GetInt32());
    }

    [Fact]
    public async Task ListRateLimitPolicies_MarksTheEnabledPoliciesThatProtectSignIn()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRateLimitPoliciesTool(), services));

        // Assert: the global policy and the policy on the group of the sign-in endpoints.
        var protecting = result.GetProperty("items").EnumerateArray()
            .Where(item => item.TryGetProperty("protectsSignIn", out var protects) && protects.GetBoolean())
            .Select(item => item.GetProperty("id").GetString());

        Assert.Equal(["policy-login", "policy-global"], protecting);
    }

    [Theory]
    [InlineData("status", "disabled", "policy-draft")]
    [InlineData("search", "contact form", "policy-contact")]
    [InlineData("search", "password-auth", "policy-login")]
    public async Task ListRateLimitPolicies_WithAFilter_KeepsTheMatchingPolicies(string argument, string value, string expectedId)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRateLimitPoliciesTool(), services, new()
        {
            [argument] = value,
        }));

        // Assert
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal(expectedId, item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SetRateLimitPolicyEnabled_Enabling_EnablesTheDisabledPoliciesAndReloadsTheTenantOnce()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-draft", "policy-contact", "policy-missing" },
            ["enabled"] = true,
        }));

        // Assert
        var enabled = Assert.Single(result.GetProperty("changed").EnumerateArray());

        Assert.Equal("policy-draft", enabled.GetProperty("id").GetString());
        Assert.True(enabled.GetProperty("enabled").GetBoolean());
        Assert.Equal(["Contact API"], result.GetProperty("alreadySet").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(["policy-missing"], result.GetProperty("notFound").EnumerateArray().Select(id => id.GetString()));
        Assert.True(result.GetProperty("tenantReloads").GetBoolean());
        Assert.True(harness.RateLimits.Draft.IsEnabled);
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task SetRateLimitPolicyEnabled_WhenNothingChanges_DoesNotReloadTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact", "policy-missing" },
            ["enabled"] = true,
        }));

        // Assert
        Assert.Empty(result.GetProperty("changed").EnumerateArray());
        Assert.False(result.TryGetProperty("tenantReloads", out _));
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetRateLimitPolicyEnabled_DisablingAPolicyThatDoesNotProtectSignIn_DisablesItAndReloadsTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact", "policy-draft" },
            ["enabled"] = false,
        }));

        // Assert
        var disabled = Assert.Single(result.GetProperty("changed").EnumerateArray());

        Assert.False(result.GetProperty("enabled").GetBoolean());

        Assert.Equal("policy-contact", disabled.GetProperty("id").GetString());
        Assert.Equal(["Draft policy"], result.GetProperty("alreadySet").EnumerateArray().Select(name => name.GetString()));
        Assert.False(result.TryGetProperty("signInProtectionRemoved", out _));
        Assert.False(harness.RateLimits.ContactApi.IsEnabled);
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Theory]
    [InlineData("policy-global", "is a global policy")]
    [InlineData("policy-login", "limits the group 'password-authentication'")]
    public async Task SetRateLimitPolicyEnabled_DisablingAPolicyThatProtectsSignIn_NeedsTheConfirmation(string policyId, string reason)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            // The policy that does not protect sign-in is not disabled either: the call is checked as a whole.
            ["policyIds"] = new[] { "policy-contact", policyId },
            ["enabled"] = false,
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, arguments);
        var enabledAfterRefusal = harness.RateLimits.Policies.Count(policy => policy.IsEnabled);
        var reloadsAfterRefusal = harness.RateLimits.ReleaseRequests;

        arguments["confirmRemovesSignInProtection"] = true;

        var confirmed = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, arguments));

        // Assert
        Assert.StartsWith("Nothing was changed:", refused, StringComparison.Ordinal);
        Assert.Contains(reason, refused, StringComparison.Ordinal);
        Assert.Contains("confirmRemovesSignInProtection", refused, StringComparison.Ordinal);
        Assert.Equal(3, enabledAfterRefusal);
        Assert.Equal(0, reloadsAfterRefusal);
        Assert.Equal(2, confirmed.GetProperty("changed").GetArrayLength());
        Assert.Contains(reason, Assert.Single(confirmed.GetProperty("signInProtectionRemoved").EnumerateArray()).GetString(), StringComparison.Ordinal);
        Assert.Equal(1, harness.RateLimits.Policies.Count(policy => policy.IsEnabled));
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Theory]
    [InlineData("/Login", true)]
    [InlineData("/login/", true)]
    [InlineData("/", true)]
    [InlineData("/Log", false)]
    [InlineData("/api", false)]
    public async Task SetRateLimitPolicyEnabled_DisablingAnEndpointPolicy_AsksWhenItsPathCoversASignInEndpoint(string path, bool protects)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.RateLimits.ContactApi.Path = path;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact" },
            ["enabled"] = false,
        });

        // Assert: the rate limiter matches an endpoint policy on whole path segments, so "/Log" does not cover "/Login".
        Assert.Equal(protects, result.StartsWith("Nothing was changed:", StringComparison.Ordinal));
        Assert.Equal(protects, harness.RateLimits.ContactApi.IsEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetRateLimitPolicyEnabled_WithoutIds_ChangesNothing(bool enabled)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = Array.Empty<string>(),
            ["enabled"] = enabled,
        });

        // Assert
        Assert.StartsWith("Nothing was changed:", result, StringComparison.Ordinal);
        Assert.Contains("give policyIds", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SetRateLimitPolicyEnabled_WithoutEnabled_ChangesNothing()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetRateLimitPolicyEnabledTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-draft" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: give enabled", result, StringComparison.Ordinal);
        Assert.False(harness.RateLimits.Draft.IsEnabled);
        harness.VerifyNothingChanged();
    }
}

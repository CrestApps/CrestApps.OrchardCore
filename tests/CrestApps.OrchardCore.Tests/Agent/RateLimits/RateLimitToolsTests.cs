using System.Text.Json;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using CrestApps.OrchardCore.Tests.Agent.Security;
using OrchardCore.Entities;
using OrchardCore.RateLimits.Models;

namespace CrestApps.OrchardCore.Tests.Agent.RateLimits;

/// <summary>
/// The rate-limit tools work on the policy store of the Rate Limits admin page with the rules of that page: a new
/// policy is saved disabled, the target and the limiters of an enabled policy cannot be changed, and the tenant
/// reloads when a policy starts or stops applying. A policy that protects the sign-in and account endpoints is not
/// disabled or deleted unless the call says so.
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
    public async Task SaveRateLimitPolicy_WithoutAnId_CreatesADisabledPolicyAndDoesNotReloadTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveRateLimitPolicyTool(), services, new()
        {
            ["name"] = "Search API",
            ["description"] = "Protects the search.",
            ["scope"] = "endpoint",
            ["path"] = "/api/find",
            ["limiters"] = """
                [
                  { "type": "slidingWindow", "permitLimit": 30, "windowSeconds": 60, "segmentsPerWindow": 6, "queueLimit": 2 },
                  { "type": "tokenBucket", "tokenLimit": 100, "tokensPerPeriod": 10, "replenishmentPeriodSeconds": 5, "queueProcessingOrder": "newestFirst" }
                ]
                """,
        }));

        // Assert
        var policy = Assert.Single(harness.RateLimits.Policies, stored => stored.Name == "Search API");
        var sliding = policy.Limiters[0].GetOrCreate<SlidingWindowRateLimiterData>();
        var bucket = policy.Limiters[1].GetOrCreate<TokenBucketRateLimiterData>();

        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.False(policy.IsEnabled);
        Assert.Null(policy.EnabledUtc);
        Assert.Equal(0, harness.RateLimits.ReleaseRequests);
        Assert.Equal(RateLimitPolicyScope.Endpoint, policy.Scope);
        Assert.Equal("/api/find", policy.Path);
        Assert.Null(policy.GroupName);
        Assert.Equal("Protects the search.", policy.Description);
        Assert.Equal(AgentToolTestContext.UserId, policy.OwnerId);
        Assert.Equal(AgentToolTestContext.UserName, policy.Author);
        Assert.Equal(["SlidingWindow", "TokenBucket"], policy.Limiters.Select(limiter => limiter.Source));
        Assert.All(policy.Limiters, limiter => Assert.False(string.IsNullOrEmpty(limiter.Id)));
        Assert.Equal((30, 60, 6, 2), (sliding.PermitLimit, sliding.WindowSeconds, sliding.SegmentsPerWindow, sliding.QueueLimit));
        Assert.Equal((100, 10, 5, 0), (bucket.TokenLimit, bucket.TokensPerPeriod, bucket.ReplenishmentPeriodSeconds, bucket.QueueLimit));
        Assert.Equal(QueueProcessingOrder.NewestFirst, bucket.QueueProcessingOrder);
        Assert.False(result.GetProperty("policy").GetProperty("enabled").GetBoolean());
        Assert.Contains("enableRateLimitPolicy", result.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "name": "Contact API", "scope": "global" }""", "a policy named 'Contact API' exists")]
    [InlineData("""{ "name": "New", "scope": "endpoint", "path": "api/x" }""", "the request path must start with '/'")]
    [InlineData("""{ "name": "New", "scope": "endpoint" }""", "give path for an endpoint policy")]
    [InlineData("""{ "name": "New", "scope": "group" }""", "give group for a group policy")]
    [InlineData("""{ "name": "New" }""", "give name and scope for a new policy")]
    [InlineData("""{ "name": "New", "scope": "site" }""", "scope must be global, endpoint or group")]
    [InlineData("""{ "name": "New", "scope": "global", "limiters": [ { "type": "fixedWindow", "permitLimit": 0, "windowSeconds": 60 } ] }""", "limiter 1: permitLimit must be a whole number of at least 1")]
    [InlineData("""{ "name": "New", "scope": "global", "limiters": [ { "type": "fixedWindow", "permitLimit": 5 } ] }""", "limiter 1: give windowSeconds")]
    [InlineData("""{ "name": "New", "scope": "global", "limiters": [ { "type": "concurrency", "permitLimit": 5, "queueLimit": -1 } ] }""", "limiter 1: queueLimit must be a whole number of at least 0")]
    [InlineData("""{ "name": "New", "scope": "global", "limiters": [ { "type": "leakyBucket", "permitLimit": 5 } ] }""", "limiter 1: type must be one of")]
    public async Task SaveRateLimitPolicy_WithWhatTheAdminPageRefuses_SavesNothing(string json, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRateLimitPolicyTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("""{ "policyId": "policy-contact", "limiters": [ { "type": "fixedWindow", "permitLimit": 1000, "windowSeconds": 1 } ] }""")]
    [InlineData("""{ "policyId": "policy-contact", "path": "/api" }""")]
    [InlineData("""{ "policyId": "policy-contact", "scope": "global" }""")]
    public async Task SaveRateLimitPolicy_ThatChangesTheTargetOrTheLimitersOfAnEnabledPolicy_IsRefused(string json)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRateLimitPolicyTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved: the policy 'Contact API' is enabled", result, StringComparison.Ordinal);
        Assert.Contains("disableRateLimitPolicy", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveRateLimitPolicy_ForAnEnabledPolicy_ChangesItsNameAndDescriptionOnly()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveRateLimitPolicyTool(), services, new()
        {
            ["policyId"] = "policy-contact",
            ["name"] = "Contact form",
            ["description"] = "Ten messages a minute.",
        }));

        // Assert
        var policy = Assert.Single(harness.RateLimits.Policies, stored => stored.PolicyId == "policy-contact");

        Assert.False(result.GetProperty("created").GetBoolean());
        Assert.Equal("Contact form", policy.Name);
        Assert.Equal("Ten messages a minute.", policy.Description);
        Assert.True(policy.IsEnabled);
        Assert.Equal("/api/contact", policy.Path);
        Assert.Equal("user-owner", policy.OwnerId);
        Assert.Single(policy.Limiters);
        Assert.Equal(0, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task SaveRateLimitPolicy_ForADisabledPolicy_ReplacesItsTargetAndLimiters()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveRateLimitPolicyTool(), services, new()
        {
            ["policyId"] = "policy-draft",
            ["scope"] = "group",
            ["group"] = "newsletter",
            ["limiters"] = """[ { "type": "concurrency", "permitLimit": 4 } ]""",
        }));

        // Assert
        var policy = Assert.Single(harness.RateLimits.Policies, stored => stored.PolicyId == "policy-draft");
        var limiter = Assert.Single(policy.Limiters);

        Assert.Equal(RateLimitPolicyScope.Group, policy.Scope);
        Assert.Equal("newsletter", policy.GroupName);
        Assert.Null(policy.Path);
        Assert.Equal("Concurrency", limiter.Source);
        Assert.Equal(4, limiter.GetOrCreate<ConcurrencyRateLimiterData>().PermitLimit);
        Assert.False(policy.IsEnabled);

        // No endpoint of the site is in that group, so the policy would limit nothing.
        var warning = Assert.Single(result.GetProperty("warnings").EnumerateArray());

        Assert.Contains("No endpoint of the site is in the rate-limit group 'newsletter'", warning.GetString(), StringComparison.Ordinal);
        Assert.Contains("contact-forms, password-authentication", warning.GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnableRateLimitPolicy_EnablesTheDisabledPoliciesAndReloadsTheTenantOnce()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new EnableRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-draft", "policy-contact", "policy-missing" },
        }));

        // Assert
        var enabled = Assert.Single(result.GetProperty("enabled").EnumerateArray());

        Assert.Equal("policy-draft", enabled.GetProperty("id").GetString());
        Assert.True(enabled.GetProperty("enabled").GetBoolean());
        Assert.Equal(["Contact API"], result.GetProperty("alreadyEnabled").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(["policy-missing"], result.GetProperty("notFound").EnumerateArray().Select(id => id.GetString()));
        Assert.True(result.GetProperty("tenantReloads").GetBoolean());
        Assert.True(harness.RateLimits.Draft.IsEnabled);
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task EnableRateLimitPolicy_WhenNothingChanges_DoesNotReloadTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new EnableRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact", "policy-missing" },
        }));

        // Assert
        Assert.Empty(result.GetProperty("enabled").EnumerateArray());
        Assert.False(result.TryGetProperty("tenantReloads", out _));
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DisableRateLimitPolicy_ForAPolicyThatDoesNotProtectSignIn_DisablesItAndReloadsTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DisableRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact", "policy-draft" },
        }));

        // Assert
        var disabled = Assert.Single(result.GetProperty("disabled").EnumerateArray());

        Assert.Equal("policy-contact", disabled.GetProperty("id").GetString());
        Assert.Equal(["Draft policy"], result.GetProperty("alreadyDisabled").EnumerateArray().Select(name => name.GetString()));
        Assert.False(result.TryGetProperty("signInProtectionRemoved", out _));
        Assert.False(harness.RateLimits.ContactApi.IsEnabled);
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Theory]
    [InlineData("policy-global", "is a global policy")]
    [InlineData("policy-login", "limits the group 'password-authentication'")]
    public async Task DisableRateLimitPolicy_ForAPolicyThatProtectsSignIn_NeedsTheConfirmation(string policyId, string reason)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            // The policy that does not protect sign-in is not disabled either: the call is checked as a whole.
            ["policyIds"] = new[] { "policy-contact", policyId },
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new DisableRateLimitPolicyTool(), services, arguments);
        var enabledAfterRefusal = harness.RateLimits.Policies.Count(policy => policy.IsEnabled);
        var reloadsAfterRefusal = harness.RateLimits.ReleaseRequests;

        arguments["confirmRemovesSignInProtection"] = true;

        var confirmed = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DisableRateLimitPolicyTool(), services, arguments));

        // Assert
        Assert.StartsWith("Nothing was disabled:", refused, StringComparison.Ordinal);
        Assert.Contains(reason, refused, StringComparison.Ordinal);
        Assert.Contains("confirmRemovesSignInProtection", refused, StringComparison.Ordinal);
        Assert.Equal(3, enabledAfterRefusal);
        Assert.Equal(0, reloadsAfterRefusal);
        Assert.Equal(2, confirmed.GetProperty("disabled").GetArrayLength());
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
    public async Task DisableRateLimitPolicy_ForAnEndpointPolicy_AsksWhenItsPathCoversASignInEndpoint(string path, bool protects)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.RateLimits.ContactApi.Path = path;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DisableRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact" },
        });

        // Assert: the rate limiter matches an endpoint policy on whole path segments, so "/Log" does not cover "/Login".
        Assert.Equal(protects, result.StartsWith("Nothing was disabled:", StringComparison.Ordinal));
        Assert.Equal(protects, harness.RateLimits.ContactApi.IsEnabled);
    }

    [Fact]
    public async Task DeleteRateLimitPolicy_ForADisabledPolicy_DeletesItWithoutReloadingTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-draft", "policy-missing" },
        }));

        // Assert
        var deleted = Assert.Single(result.GetProperty("deleted").EnumerateArray());

        Assert.Equal("policy-draft", deleted.GetProperty("id").GetString());
        Assert.False(deleted.GetProperty("wasEnabled").GetBoolean());
        Assert.Equal(["policy-missing"], result.GetProperty("notFound").EnumerateArray().Select(id => id.GetString()));
        Assert.False(result.TryGetProperty("tenantReloads", out _));
        Assert.DoesNotContain(harness.RateLimits.Policies, policy => policy.PolicyId == "policy-draft");
        Assert.Equal(0, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task DeleteRateLimitPolicy_ForAnEnabledPolicy_ReloadsTheTenant()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-contact" },
        }));

        // Assert
        Assert.True(result.GetProperty("tenantReloads").GetBoolean());
        Assert.DoesNotContain(harness.RateLimits.Policies, policy => policy.PolicyId == "policy-contact");
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task DeleteRateLimitPolicy_ForAnEnabledPolicyThatProtectsSignIn_NeedsTheConfirmation()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["policyIds"] = new[] { "policy-login", "policy-draft" },
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new DeleteRateLimitPolicyTool(), services, arguments);
        var policiesAfterRefusal = harness.RateLimits.Policies.Count;

        arguments["confirmRemovesSignInProtection"] = true;

        var confirmed = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteRateLimitPolicyTool(), services, arguments));

        // Assert
        Assert.StartsWith("Nothing was deleted: 'Login throttle' limits the group 'password-authentication' of the sign-in and account endpoints.", refused, StringComparison.Ordinal);
        Assert.Equal(4, policiesAfterRefusal);
        Assert.Equal(2, confirmed.GetProperty("deleted").GetArrayLength());
        Assert.Single(confirmed.GetProperty("signInProtectionRemoved").EnumerateArray());
        Assert.Equal(2, harness.RateLimits.Policies.Count);
        Assert.Equal(1, harness.RateLimits.ReleaseRequests);
    }

    [Fact]
    public async Task DeleteRateLimitPolicy_ForADisabledPolicyOnTheSignInGroup_DoesNotAsk()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.RateLimits.LoginThrottle.IsEnabled = false;

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteRateLimitPolicyTool(), services, new()
        {
            ["policyIds"] = new[] { "policy-login" },
        }));

        // Assert: a disabled policy protects nothing.
        Assert.Single(result.GetProperty("deleted").EnumerateArray());
        Assert.Equal(0, harness.RateLimits.ReleaseRequests);
    }

    [Theory]
    [InlineData(EnableRateLimitPolicyTool.TheName)]
    [InlineData(DisableRateLimitPolicyTool.TheName)]
    [InlineData(DeleteRateLimitPolicyTool.TheName)]
    public async Task ATool_ThatActsOnSeveralPolicies_WithoutIds_ChangesNothing(string toolName)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var (tool, _) = SecurityToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, new()
        {
            ["policyIds"] = Array.Empty<string>(),
        });

        // Assert
        Assert.StartsWith("Nothing was", result, StringComparison.Ordinal);
        Assert.Contains("give policyIds", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }
}

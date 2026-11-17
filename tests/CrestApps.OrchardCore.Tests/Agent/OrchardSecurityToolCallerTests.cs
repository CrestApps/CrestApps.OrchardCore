using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.AuditTrail;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tests.Agent.Security;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every OpenID, rate limit, secret, audit trail, notification, test email and test SMS tool acts for the person in
/// the chat: it refuses a caller who is not signed in and a caller who does not hold the permission the matching
/// admin action asks for, and it changes nothing when it refuses. No tool returns the value of a secret, a client
/// secret, a private key, a field of a user snapshot a site may record, or a record of another user. Each JSON schema
/// takes no argument it does not declare, describes each argument and has no argument for a secret.
/// </summary>
public sealed class OrchardSecurityToolCallerTests
{
    public static TheoryData<string> ToolNames => [.. SecurityToolCalls.ToolNames];

    [Fact]
    public void TheBatch_HasTwentyThreeTools()
    {
        // Assert
        Assert.Equal(23, SecurityToolCalls.ToolNames.Length);
        Assert.Equal(23, SecurityToolCalls.ToolNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var harness = new SecurityToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();

        var (tool, arguments) = SecurityToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminAction_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var harness = new SecurityToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();

        var (tool, arguments) = SecurityToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert: one check, of the permission of the admin action, without a resource as on the admin page.
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(SecurityToolCalls.GetPermission(toolName), check.Permission);
        Assert.Null(check.Resource);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenEverySecretFieldOfTheFixtureIsSet_ReturnsNoneOfThem(string toolName)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        // The fixture is what the test says it is: every field a tool must not return holds a canary.
        Assert.StartsWith(SecurityToolHarness.Canary, harness.OpenId.Backend.ClientSecret, StringComparison.Ordinal);
        Assert.StartsWith(SecurityToolHarness.Canary, Assert.IsType<TextSecret>(harness.Secrets.Database.Peek("Smtp.Password")).Text, StringComparison.Ordinal);
        Assert.StartsWith(SecurityToolHarness.Canary, Assert.IsType<RsaKeySecret>(harness.Secrets.Database.Peek("OpenId.SigningKey")).PrivateKey, StringComparison.Ordinal);
        Assert.Contains(SecurityToolHarness.Canary, Assert.IsType<X509Secret>(harness.Secrets.Database.Peek("Site.Certificate")).PrivateKeyPem, StringComparison.Ordinal);
        Assert.StartsWith(SecurityToolHarness.Canary, Assert.IsType<TextSecret>(harness.Secrets.Configuration.Peek("Twilio.AuthToken")).Text, StringComparison.Ordinal);
        Assert.Contains(harness.AuditTrail.Events, auditTrailEvent => auditTrailEvent.Properties.ToJsonString().Contains($"{SecurityToolHarness.Canary}-SECURITY-STAMP", StringComparison.Ordinal));
        Assert.Contains(harness.AuditTrail.Events, auditTrailEvent => auditTrailEvent.Properties.ToJsonString().Contains($"{SecurityToolHarness.Canary}-CONTENT-FIELD", StringComparison.Ordinal));
        Assert.Contains(harness.AuditTrail.Events, auditTrailEvent => auditTrailEvent.Properties.ToJsonString().Contains($"{SecurityToolHarness.Canary}-CUSTOM-EVENT-DATA", StringComparison.Ordinal));
        Assert.Contains(harness.Notifications.Notifications, notification => notification.UserId != AgentToolTestContext.UserId && notification.Subject.StartsWith(SecurityToolHarness.Canary, StringComparison.Ordinal));

        // The list of notifications is given the notifications of every user, as a faulty query would.
        harness.Notifications.QueryReturnsEveryUser = true;

        using var services = harness.Build();

        var (tool, arguments) = SecurityToolCalls.Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.NotEqual(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.DoesNotContain(SecurityToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase);

        // The call ran: the result is the JSON of a tool that did its work, not a refusal that names nothing.
        Assert.Equal(JsonValueKind.Object, AgentToolTestContext.ReadJson(result).ValueKind);

        // No step the tools ran carries a secret either.
        Assert.All(harness.OpenId.Steps, step => Assert.DoesNotContain(SecurityToolHarness.Canary, step.ToJsonString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAuditTrailEvent_ForEveryEventOfTheFixture_ReturnsNoneOfItsStoredSecrets()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var (tool, arguments) = SecurityToolCalls.Create(GetAuditTrailEventTool.TheName);
        var results = new List<string>();

        // Act: each event of the fixture holds a canary in its stored data.
        foreach (var auditTrailEvent in harness.AuditTrail.Events)
        {
            arguments["eventId"] = auditTrailEvent.EventId;
            results.Add(await AgentToolTestContext.InvokeAsync(tool, services, arguments));
        }

        // Assert
        Assert.Equal(5, results.Count);
        Assert.All(results, result => Assert.DoesNotContain(SecurityToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase));
        Assert.All(results, result => Assert.Equal(JsonValueKind.Object, AgentToolTestContext.ReadJson(result).ValueKind));
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsDescribesEachOneAndTakesNoSecret(string toolName)
    {
        // Arrange
        var (tool, _) = SecurityToolCalls.Create(toolName);

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

        // Anything a model sends has been through the AI provider and the chat history, so no tool takes the value of
        // a secret, a client secret, a key, a token, a password, a certificate or a connection string.
        Assert.All(properties, property =>
        {
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pem", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionString", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(property.Name.EndsWith("password", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.False(property.Name.EndsWith("token", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.False(property.Name.EndsWith("key", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.False(property.Name.Equals("value", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.False(property.Name.Equals("text", StringComparison.OrdinalIgnoreCase) && toolName.Contains("Secret", StringComparison.Ordinal), property.Name);
        });
    }
}

using CrestApps.OrchardCore.AI.Agent.Secrets;
using CrestApps.OrchardCore.Tests.Agent.Security;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.Agent.Secrets;

/// <summary>
/// The secret tool lists what the secret stores say about their secrets and never returns the value of a secret, a
/// private key or PEM material.
/// </summary>
public sealed class SecretToolsTests
{
    [Fact]
    public async Task ListSecrets_ReturnsWhatTheStoresSayAboutTheSecretsWithoutReadingAValue()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services);

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var smtp = items.Single(item => item.GetProperty("name").GetString() == "Smtp.Password");

        Assert.Equal(4, result.GetProperty("total").GetInt32());
        Assert.Equal(["OpenId.SigningKey", "Site.Certificate", "Smtp.Password", "Twilio.AuthToken"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("Database", smtp.GetProperty("store").GetString());
        Assert.Equal("TextSecret", smtp.GetProperty("type").GetString());
        Assert.Equal("The password of the SMTP account.", smtp.GetProperty("description").GetString());
        Assert.Equal("2026-11-27T12:00:00+00:00", smtp.GetProperty("expiresAt").GetString());
        Assert.True(smtp.GetProperty("expiresWithin30Days").GetBoolean());
        Assert.Equal(1, result.GetProperty("expiredCount").GetInt32());
        Assert.Equal(1, result.GetProperty("expiringWithin30DaysCount").GetInt32());

        // Every field of a secret is one the store tells about it; none is its value.
        Assert.All(items, item => Assert.Subset(
            new HashSet<string> { "name", "store", "type", "description", "createdAt", "updatedAt", "expiresAt", "expired", "expiresWithin30Days" },
            item.EnumerateObject().Select(property => property.Name).ToHashSet()));

        var stores = result.GetProperty("stores").EnumerateArray().ToDictionary(store => store.GetProperty("name").GetString(), store => store.GetProperty("readOnly").GetBoolean());

        Assert.Equal(3, stores.Count);
        Assert.False(stores["Database"]);
        Assert.True(stores["Configuration"]);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("expiry", "expired", "OpenId.SigningKey")]
    [InlineData("expiry", "expiringSoon", "Smtp.Password")]
    [InlineData("type", "X509Secret", "Site.Certificate")]
    [InlineData("search", "configuration", "Twilio.AuthToken")]
    [InlineData("search", "signs", "OpenId.SigningKey")]
    public async Task ListSecrets_WithAFilter_KeepsTheMatchingSecrets(string argument, string value, string expectedName)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services, new()
        {
            [argument] = value,
        }));

        // Assert
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal(expectedName, item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListSecrets_ThatNeverExpire_AreTheOnesWithoutAnExpiryDate()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services, new()
        {
            ["expiry"] = "neverExpires",
        }));

        // Assert
        Assert.Equal(["Site.Certificate", "Twilio.AuthToken"], result.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }
}
